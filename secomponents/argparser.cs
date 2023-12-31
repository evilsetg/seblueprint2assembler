using System;
using System.Collections.Generic;

namespace Argparser
{
    class ArgParser
    {
        public List<Argument> Arguments;
        public List<ArgumentPositional> PositionalArguments;
        public List<ArgumentOption> OptionArguments; 
        public List<string> ParsedPositionalArguments;
        public Dictionary<string, List<string>> ParsedOptionArguments; 

        public ArgParser()
        {
            Arguments = new List<Argument>();
            PositionalArguments = new List<ArgumentPositional>();
            OptionArguments = new List<ArgumentOption>();
            ParsedPositionalArguments = new List<string>();
            ParsedOptionArguments = new Dictionary<string, List<string>>();
        }

        public abstract class Argument
        {
            public string Description {get;}
            public bool Optional {get;}
            public List<string> Claims {get;set;}

            public Argument(bool optional, string description = "")
            {
                Description = description;
                Optional = optional;
                Claims = new List<string>();
            }

            public abstract string ToString();
            
            public virtual string ToString(string child_desc)
            {
                return (Optional ? $"[{child_desc}]" : $"{child_desc}") + " - " + Description;
            }
            public virtual Tuple<int,int> ClaimArg(string arg)
            {
                return new Tuple<int,int>(0,0);
            }
        }

        public class ArgumentOption: Argument
        {
            public string[] Markers;
            int Nargs;
            public ArgumentOption(string[] markers,
                                  int nargs,
                                  bool optional,
                                  string description = ""): base(optional, description)
            {
                Markers = markers;
                Nargs = nargs;
            }
            public override string ToString()
            {
                string smarkers = string.Join('|', Markers);
                return base.ToString($"{smarkers} {Nargs}");
            }
            public override Tuple<int,int> ClaimArg(string arg)
            {
                foreach (string marker in Markers)
                {
                    if (arg.StartsWith(marker))
                        return new Tuple<int,int>(-1,Nargs+1);
                }
                return new Tuple<int,int>(0,0);
                
            }
        }

        public class ArgumentPositional: Argument
        {
            public int Position;
            public ArgumentPositional(int position,
                                      bool optional,
                                      string description = ""): base(optional, description)
            {
                Position = position;
            }
            public override string ToString()
            {
                return base.ToString($"{Position}");
            }
            public override Tuple<int,int> ClaimArg(string arg)
            {
                if (arg.StartsWith("-") || Claims.Count != 0)
                    return new Tuple<int,int>(0,0);
                else
                    return new Tuple<int,int>(Position,1);
            }
        }

        static Argument ParseArgumentString(string sarg, string description="")
        {
            bool optional = false;
            string[] subargs = sarg.Split(' ');

            if (subargs[0].StartsWith('['))
            {
                optional = true;
                subargs[0] = subargs[0].Trim( new[] { '[', ']' } );
            }

            if (subargs[0].StartsWith("-"))
            {
                return new ArgumentOption(subargs[0].Split('|'),
                                          subargs.Length - 1,
                                          optional,
                                          description);
            }
            else
            {
                return new ArgumentPositional(int.Parse(subargs[0].TrimEnd(':')),
                                              optional,
                                              description);
            }
        }

        int compareFirst(Tuple<int,int,Argument> x, Tuple<int,int,Argument> y)
        {
            if (x.Item1 == y.Item1)
                return 0;
            else if (x.Item1 < x.Item1)
                return -1;
            else
                return 1;
        }

        public void ClaimArgs(string[] args)
        {
            List<Tuple<int,int,Argument>> claims = new List<Tuple<int,int,Argument>>();
            for (int i=0; i < args.Length; i++)
            {
                claims.Clear();
                foreach(Argument option in Arguments)
                {
                    Tuple<int,int> claim = option.ClaimArg(args[i]);
                    if (claim.Item2 > 0)
                        claims.Add(new Tuple<int,int,Argument>(claim.Item1,claim.Item2,option));
                }
                claims.Sort(compareFirst);
                if (claims.Count == 0)
                    throw new InvalidOperationException($"Argument {args[i]} not recognized.");
                int claimNargs = claims[0].Item2;
                Argument claimer = claims[0].Item3;
                ArraySegment<string> claimedArgs = new ArraySegment<string>(args,i,claimNargs);
                claimer.Claims.AddRange(claimedArgs);

                string sclaimed_args = string.Join(' ',claimedArgs);
                // Console.WriteLine($"{sclaimed_args} claimed by {claimer.ToString()}");

                i+=claimNargs-1;
            }
        }

        int SortPositionalArguments(ArgumentPositional x, ArgumentPositional y)
        {
            if (x.Position == y.Position)
                return 0;
            else if (x.Position < y.Position)
                return -1;
            else
                return 1;
        }

        public void ParseArgs(string[] args)
        {
            ClaimArgs(args);
            foreach (Argument arg in Arguments)
            {
                if ( ! arg.Optional && arg.Claims.Count < 1 )
                    throw new InvalidOperationException($"non-optional Argument {arg.ToString()} not given");
            }
            
            foreach (ArgumentOption arg in OptionArguments)
            {
                if (arg.Claims.Count > 0)
                {
                    foreach (string marker in arg.Markers)
                    {
                        ParsedOptionArguments.Add(marker,
                                                  arg.Claims.GetRange(1,arg.Claims.Count-1));
                    }
                }
            }

            PositionalArguments.Sort(SortPositionalArguments);
            foreach (ArgumentPositional arg in PositionalArguments)
            {
                if (arg.Claims.Count > 0)
                    ParsedPositionalArguments.Add(arg.Claims[0]);
            }
        }

        public void AddArgument(string sargument, string description="")
        {
            Argument newArgument = ParseArgumentString(sargument, description);
            Arguments.Add(newArgument);
            ArgumentOption newArgumentOptional = newArgument as ArgumentOption;
            if (newArgumentOptional != null)
                OptionArguments.Add(newArgumentOptional);
            ArgumentPositional newArgumentPositional = newArgument as ArgumentPositional;
            if (newArgumentPositional != null)
                PositionalArguments.Add(newArgumentPositional);
        }

        public string this[int index]
        {
            get => ParsedPositionalArguments[index];
        }

        public List<string> this[string index]
        {
            get => ParsedOptionArguments[index];
        }

        public List<string> TryGet(string option)
        {
            List<string> argument;
            ParsedOptionArguments.TryGetValue(option, out argument);
            return argument;
        }
        public bool IsSet(string option)
        {
            return ParsedOptionArguments.ContainsKey(option);
        }
    }
}
