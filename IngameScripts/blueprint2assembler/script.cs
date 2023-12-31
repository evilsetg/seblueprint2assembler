// SE_Block class
// A block has an ID, SubID and Component dict
// ID and SubID make up the DefinitionID
public struct SE_block
{
    public SE_block(string id, string sid, List<MyTuple<MyDefinitionId,int>> comps)
    {
        TypeID = id;
        SubtypeID = sid;
        Components = comps;
        dID = MyDefinitionId.Parse("MyObjectBuilder_" + TypeID + "/" + SubtypeID);
    }

    public string TypeID { get; }
    public string SubtypeID { get; }
    public MyDefinitionId dID { get; }
    public List<MyTuple<MyDefinitionId,int>> Components { get; }

    // Print Block and it's components
    public override string ToString()
    {
        string res = "";
        res += $"{dID}\n";
        foreach (MyTuple<MyDefinitionId,int> comp_tup in Components)
        {
            res += $"{comp_tup.Item1}: {comp_tup.Item2}\n";
        }
        return res;
    }

    // build single Block object from output of secomponents.exe
    // That would be something like
    // Assembler/BasicAssembler
    // 0:60
    // 1:40
    // 5:10
    // 6:4
    // 3:80
    // 0:20
    // Where the integers are component id and amount to be resolved by component_translator
    public static SE_block FromString(string s, Dictionary<int,MyDefinitionId> component_translator)
    {
        List<MyTuple<MyDefinitionId,int>> comps = new List<MyTuple<MyDefinitionId,int>>();
        string[] lines = s.Split('\n');
        string[] identifiers = lines[0].Split('/');
        string TypeID = identifiers[0];
        string SubtypeID = identifiers[1];
        
        for (int i = 1; i < lines.Length; i++)
        {
            string[] ssplit_components = lines[i].Split(':');
            MyTuple<MyDefinitionId,int> component = new MyTuple<MyDefinitionId,int>
                (component_translator[int.Parse(ssplit_components[0])], int.Parse(ssplit_components[1]));
            comps.Add(component);
        }
        return new SE_block(TypeID, SubtypeID, comps);
    }
}

// Get dictionary of remaning blocks from projector
// A bit tricky since proj.RemainingBlocksPerType returns a forbidden class
public static Dictionary<MyDefinitionId, int> fRemainingBlocks(IMyProjector proj)
{
    Dictionary<MyDefinitionId, int> dic = new Dictionary<MyDefinitionId, int>();
    
    foreach(var kvp in proj.RemainingBlocksPerType)
    {
        MyDefinitionId part = new MyDefinitionId();
        MyDefinitionId.TryParse(kvp.Key.ToString(), out part);
        dic.Add(part,kvp.Value);
    }

    return dic;
}

// Calculate components needed to build a block
// uses global dictionary blockdic
Dictionary<MyDefinitionId,int> CalcComponents(Dictionary<MyDefinitionId,int> ReqBlocks)
{
    Dictionary<MyDefinitionId,int> dic = new Dictionary<MyDefinitionId,int>();
    foreach (KeyValuePair<MyDefinitionId,int> kvp in ReqBlocks)
    {
        List<MyTuple<MyDefinitionId,int>> Components = blockdic[kvp.Key].Components;
        foreach (MyTuple<MyDefinitionId,int> tup in Components)
        {
            if (dic.Keys.Contains(tup.Item1))
            {
                dic[tup.Item1] += tup.Item2 * kvp.Value;
            }
            else
            {
                dic.Add(tup.Item1,tup.Item2 * kvp.Value);
            }
        }
    }
    return dic;
}

// Write a line to a global surface named surf
public void WriteLine(string s)
{
    if (surf != null)
    {
        surf.WriteText(s,true);
        surf.WriteText("\n",true);
    }
}

// Create dictionary of blocks from block storage
// The storage should contain the output of secomponents.exe
public Dictionary<MyDefinitionId, SE_block> create_blockdic()
{
    string[] sep_dbl_newline = { "\n\n" };
    string[] sep_trpl_dash = { "\n---\n" };
    string[] split_storage = Storage.Split(sep_trpl_dash, StringSplitOptions.None);
    string[] sblocks = split_storage[0].Split(sep_dbl_newline, StringSplitOptions.None);
    string scompbp_int = split_storage[1];
    Dictionary<int,MyDefinitionId> component_translator = new Dictionary<int,MyDefinitionId>();

    foreach (string line in scompbp_int.Split('\n'))
    {
        WriteLine(line);
        string[] linesplit = line.Split(':');
        component_translator.Add(int.Parse(linesplit[1]), MyDefinitionId.Parse(linesplit[0]));
    }

    List<SE_block> blockinfo = (from sblock in sblocks select SE_block.FromString(sblock,component_translator)).ToList();
    blockdic = new Dictionary<MyDefinitionId, SE_block>();
    foreach (SE_block block in blockinfo)
    {
        blockdic.Add(block.dID, block);
    }
    return blockdic;
}

// Our Program's properties
// Blockdic is only evaluated once in the constructor
Dictionary<MyDefinitionId, SE_block> blockdic;
IMyTextSurface surf;

public Program()
{
    blockdic = create_blockdic();
}

public void Save()
{
}

// Put components of projector argument in production
// Also show components on a surface if given
public void Main(string argument)
{
    IMyProjector projector;
    IMyAssembler assembler;

    // Parse command line
    MyCommandLine _commandLine = new MyCommandLine();
    string projector_arg = null;
    string assembler_arg = null;
    string surface_arg = null;

    if (_commandLine.TryParse(argument))
    {
        projector_arg = _commandLine.Argument(0);
        assembler_arg = _commandLine.Argument(1);
        surface_arg = _commandLine.Argument(2);

    }

    //Get blocks from command line arguments
    projector = (IMyProjector)GridTerminalSystem.GetBlockWithName(projector_arg);
    assembler = (IMyAssembler)GridTerminalSystem.GetBlockWithName(assembler_arg);
    surf = (IMyTextSurface)GridTerminalSystem.GetBlockWithName(surface_arg);

    // Clear surface
    surf?.WriteText("");

    if (assembler == null)
    {
        Echo($"Assembler \"{assembler_arg}\" not found");
        Echo("Printing components only:");
        WriteLine($"Assembler \"{assembler_arg}\" not found");
        WriteLine("Printing components only:");
    }


    // Get remaining blocks of a projection
    Dictionary<MyDefinitionId, int> proj_dic = fRemainingBlocks(projector);

    // Calculate needed compontens of those blocks
    Dictionary<MyDefinitionId, int> comp_dic = CalcComponents(proj_dic);

    //Print components and put them in production
    foreach (var kvp in comp_dic)
    {
        string component = kvp.Key.ToString().Split('/')[1];
        
        Echo($"{component}: {kvp.Value}");
        WriteLine($"{component}: {kvp.Value}");
        assembler?.AddQueueItem(kvp.Key,(decimal)kvp.Value);
    }
}
