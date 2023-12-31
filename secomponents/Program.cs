using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using Argparser;

namespace SE_Block_Components
{
    class SE_Block_Components_Class
    {
        public struct SE_block
        {
            public SE_block(string id, string sid, List<Tuple<string,int>> comps)
            {
                TypeID = id;
                SubtypeID = sid;
                Components = comps;
            }

            public string TypeID { get; }
            public string SubtypeID { get; }
            public List<Tuple<string,int>> Components { get; }

            public override string ToString()
            {
                string res = new string("");
                res += $"{TypeID}/{SubtypeID}\n";
                foreach (var comp_tup in Components)
                {
                    res += $"{comp_tup.Item1}: {comp_tup.Item2}\n";
                }
                return res;
            }

            public string ToString(Dictionary<string,int> cintdic)
            {
                string res = new string("");
                res += $"{TypeID}/{SubtypeID}\n";
                foreach (var comp_tup in Components)
                {
                    res += $"{cintdic[comp_tup.Item1]}:{comp_tup.Item2}\n";
                }
                return res;
            }
        }

        static public List<Tuple<string,int>> ParseComponents(XmlNode comps_xml)
        {
            List<Tuple<string,int>> dic = new List<Tuple<string,int>>();
            foreach (XmlNode component in comps_xml.SelectNodes("Component"))
            {
                XmlElement comp_ele = (XmlElement)component;
                string type = comp_ele.GetAttributeNode("Subtype").InnerXml;
                int count = Int32.Parse(comp_ele.GetAttributeNode("Count").InnerXml);
                dic.Add(Tuple.Create(type, count));
            }
            return dic;
        }

        static public void PrintHelp()
        {
           string helpstring = @"se_parseblocks [-h|--help] [-p|--se-path SE_GamePath] [-o|--output-path OutputPath]
Read SpaceEngineers Block components from Game files for usage in ingame scripts.
Options:
-h, --help: Print this message and do nothing.
-p, --se-path: Path of the Space Engineers game directory
-o, --output-path: The output directory for the generated script
";
           Console.WriteLine(helpstring);
        }

        const string SE_components_path_guess = @"c:\Program Files (x86)\steam\steamapps\common\SpaceEngineers\Content\Data\CubeBlocks";
        const string SE_blueprints_path_guess = @"c:\Program Files (x86)\steam\steamapps\common\SpaceEngineers\Content\Data\Blueprints.sbc";

        static void Main(string[] args)
        {
            //Parse Arguments
            string SE_components_path;
            string SE_blueprints_path;
            string OutputPath;
            ArgParser myargparser = new ArgParser();
            myargparser.AddArgument("[-h|--help]", "Print help message and exit");
            myargparser.AddArgument("[-p|--se-path] path", "The path of the SpaceEngineers game directory");
            myargparser.AddArgument("[-o|--output-path] path", "The output directory for the generated script");
            myargparser.ParseArgs(args);
            if (myargparser.IsSet("-h"))
            {
                PrintHelp();
                return;
            }
            List<string> argumentSEPath = myargparser.TryGet("-p");
            if (argumentSEPath != null)
            {
                SE_components_path = argumentSEPath[0] + @"\Content\Data\CubeBlocks";
                SE_blueprints_path = argumentSEPath[0] + @"\Content\Data\Blueprints.sbc";
            }
            else
            {
                SE_components_path = SE_components_path_guess;
                SE_blueprints_path = SE_blueprints_path_guess;
            }
            SE_components_path = Path.GetFullPath(SE_components_path);
            SE_blueprints_path = Path.GetFullPath(SE_blueprints_path);
            
            List<string> argumentOutputPath = myargparser.TryGet("-o");
            if (argumentOutputPath != null)
                OutputPath = argumentOutputPath[0];
            else
                OutputPath = ".";
            OutputPath = Path.GetFullPath(OutputPath);


            //Parse component files
            Console.WriteLine($"Parsing files in {SE_components_path}");
            List<string> files = new List<string>(Directory.EnumerateFiles(SE_components_path));
            List<SE_block> mblocks = new List<SE_block>();
            HashSet<string> component_set = new HashSet<string>();

            foreach (var file in files)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);
                XmlNodeList blocks = doc.DocumentElement.SelectNodes("CubeBlocks/Definition");
                foreach(XmlNode block in blocks)
                {
                    string typeid = block.SelectSingleNode("Id/TypeId").InnerXml;
                    string subtypeid = block.SelectSingleNode("Id/SubtypeId").InnerXml;

                    // Fix Projector having MyObjectBuilder_ in TypeId for some reason.
                    if (typeid.Contains("MyObjectBuilder_"))
                    {
                        typeid = typeid.Replace("MyObjectBuilder_", "");
                    }


                    List<Tuple<string,int>> components = ParseComponents(block.SelectSingleNode("Components"));
                    foreach (Tuple<string,int> compentry in components)
                    {
                        component_set.Add(compentry.Item1);
                    }
                    SE_block mblock = new SE_block(typeid, subtypeid, components);
                    mblocks.Add(mblock);
                }
            }

            //Parse blueprint files
            Console.WriteLine($"Parsing {SE_blueprints_path}");
            Dictionary<string,string> component_blueprints_dic = new Dictionary<string,string>();
            Dictionary<string,int> component_int_dic = new Dictionary<string,int>();

            XmlDocument bp_doc = new XmlDocument();
            bp_doc.Load(SE_blueprints_path);
            XmlNode blueprints = bp_doc.DocumentElement.SelectSingleNode("Blueprints");
            int counter = 0;
            foreach (string scomponent in component_set)
            {
                component_int_dic.Add(scomponent,counter);
                string searchstring = $"Blueprint[Result/@SubtypeId = \"{scomponent}\"]";
                XmlNode blueprint = blueprints.SelectSingleNode(searchstring);
                string typeid = blueprint.SelectSingleNode("Id/TypeId").InnerXml;
                string subtypeid = blueprint.SelectSingleNode("Id/SubtypeId").InnerXml;
                component_blueprints_dic.Add(scomponent,$"{typeid}/{subtypeid}");
                counter++;
            }


            //build output
            StringBuilder sb_blocks = new StringBuilder(100000);
            foreach (SE_block mblock in mblocks)
            {
                sb_blocks.Append(mblock.ToString(component_int_dic) + "\n");
            }
            sb_blocks.Remove(sb_blocks.Length-1,1);
            
            StringBuilder sb_components = new StringBuilder(20000);
            foreach (KeyValuePair<string,int> kvp in component_int_dic)
            {
                sb_components.Append($"{component_blueprints_dic[kvp.Key]}: {kvp.Value}\n");
            }
            sb_components.Remove(sb_components.Length-1,1);


            //write output
            using (StreamWriter sw = File.CreateText(OutputPath + @"\components.txt"))
            {
                sw.Write(sb_blocks.ToString());
                sw.Write("---\n");
                sw.Write(sb_components.ToString());
            }
            Console.WriteLine($"Wrote compontent script to {OutputPath}\\components.txt");
        }
    }
}
