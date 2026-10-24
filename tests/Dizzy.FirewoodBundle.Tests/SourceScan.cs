using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dizzy.FirewoodBundle.Tests
{
    // A light reader of the mod's C# source, enough to follow who calls whom. It is not a
    // compiler: it strips comments and strings, finds classes and the methods inside them,
    // and resolves a call by its name, either in the same class or as Class.Method. Calls on
    // a variable (item.Foo()) are game or Unity methods and are left out.
    public sealed class SourceMethod
    {
        public string File;
        public string Class;
        public string Name;
        public string Visibility;
        public string Body;
        public bool InPatch;

        public string Key { get { return Class + "." + Name; } }

        public override string ToString()
        {
            return Key + " (" + File + ")";
        }
    }

    public sealed class SourceScan
    {
        private static readonly Regex ClassHeader = new Regex(
            @"\b(?:static\s+|sealed\s+|abstract\s+|partial\s+)*(?:class|struct)\s+(?<name>\w+)[^{;]*\{", RegexOptions.Compiled);

        private static readonly Regex MethodHeader = new Regex(
            @"(?m)^[ \t]*(?:\[[^\]\n]*\][ \t]*\n?[ \t]*)*(?<vis>public\s+|internal\s+|private\s+|protected\s+)?(?:static\s+)?(?:override\s+|virtual\s+|new\s+|extern\s+)?[\w<>\[\],\.\?]+(?:[ \t]+[\w<>\[\],\.\?]+)*?[ \t]+(?<name>\w+)\s*\((?<args>[^;{}]*?)\)\s*\{",
            RegexOptions.Compiled);

        private static readonly Regex Call = new Regex(@"(?:(?<q>\w+)\s*\.\s*)?(?<n>\w+)\s*\(", RegexOptions.Compiled);

        private static readonly HashSet<string> NotMethods = new HashSet<string>
        {
            "if", "for", "while", "foreach", "switch", "using", "lock", "catch", "return", "new", "else", "fixed", "when", "nameof", "typeof",
        };

        public readonly List<SourceMethod> Methods = new List<SourceMethod>();

        public static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "src", "Dizzy.FirewoodBundle", "Plugin.cs")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("The mod's source was not found above " + AppContext.BaseDirectory);
        }

        public static SourceScan OfMod()
        {
            string folder = Path.Combine(RepoRoot(), "src", "Dizzy.FirewoodBundle");
            var scan = new SourceScan();
            foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    || path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                    continue;
                scan.Add(Path.GetFileName(path), File.ReadAllText(path));
            }
            return scan;
        }

        public static string Clean(string text)
        {
            text = text.Replace("\r\n", "\n");
            var result = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < text.Length && text[i] != '\n')
                        i++;
                }
                else if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                        i++;
                    i += 2;
                }
                else if (c == '@' && next == '"')
                {
                    i += 2;
                    while (i < text.Length)
                    {
                        if (text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                        if (text[i] == '"') break;
                        i++;
                    }
                    i++;
                    result.Append("\"\"");
                }
                else if (c == '"')
                {
                    i++;
                    while (i < text.Length && text[i] != '"')
                    {
                        if (text[i] == '\\') i++;
                        i++;
                    }
                    i++;
                    result.Append("\"\"");
                }
                else if (c == '\'' )
                {
                    i++;
                    while (i < text.Length && text[i] != '\'')
                    {
                        if (text[i] == '\\') i++;
                        i++;
                    }
                    i++;
                    result.Append("' '");
                }
                else
                {
                    result.Append(c);
                    i++;
                }
            }
            return result.ToString();
        }

        public void Add(string file, string source)
        {
            string text = Clean(source);

            var classes = new List<Tuple<string, int, int, bool>>();
            foreach (Match m in ClassHeader.Matches(text))
            {
                int open = m.Index + m.Length - 1;
                int close = MatchingBrace(text, open);
                bool patch = text.Substring(Math.Max(0, m.Index - 200), Math.Min(200, m.Index)).Contains("HarmonyPatch");
                classes.Add(Tuple.Create(m.Groups["name"].Value, open, close, patch));
            }

            foreach (Match m in MethodHeader.Matches(text))
            {
                string name = m.Groups["name"].Value;
                if (NotMethods.Contains(name))
                    continue;
                int open = m.Index + m.Length - 1;

                // The innermost class this method sits directly in.
                Tuple<string, int, int, bool> owner = classes
                    .Where(c => c.Item2 < m.Index && open < c.Item3)
                    .OrderByDescending(c => c.Item2)
                    .FirstOrDefault();
                if (owner == null)
                    continue;
                // A method is directly in its class: no other method of the class holds it.
                int close = MatchingBrace(text, open);
                Methods.Add(new SourceMethod
                {
                    File = file,
                    Class = owner.Item1,
                    Name = name,
                    Visibility = m.Groups["vis"].Success ? m.Groups["vis"].Value.Trim() : "private",
                    Body = text.Substring(open + 1, Math.Max(0, close - open - 1)),
                    InPatch = owner.Item4,
                });
            }

            // A local function or lambda body matched as a method sits inside another method.
            // Drop those, so a method's calls are read once, from the method that holds them.
        }

        private static int MatchingBrace(string text, int open)
        {
            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && --depth == 0) return i;
            }
            return text.Length - 1;
        }

        public IEnumerable<SourceMethod> Named(string cls, string name)
        {
            return Methods.Where(m => m.Class == cls && m.Name == name);
        }

        public bool HasClass(string cls)
        {
            return Methods.Any(m => m.Class == cls);
        }

        // The methods a method calls: Class.Method for a class of the mod, or Method in its own class.
        public IEnumerable<SourceMethod> Callees(SourceMethod method)
        {
            var seen = new HashSet<SourceMethod>();
            foreach (Match m in Call.Matches(method.Body))
            {
                string n = m.Groups["n"].Value;
                if (NotMethods.Contains(n))
                    continue;
                string q = m.Groups["q"].Success ? m.Groups["q"].Value : method.Class;
                if (!HasClass(q))
                    continue;
                foreach (SourceMethod target in Named(q, n))
                {
                    if (target != method && seen.Add(target))
                        yield return target;
                }
            }
        }

        // The first path from a method to one that matches, going only through methods that
        // do not stop it. Null when there is none. The path starts at the method itself.
        public List<SourceMethod> PathTo(SourceMethod from, Func<SourceMethod, bool> goal, Func<SourceMethod, bool> stop)
        {
            var parent = new Dictionary<SourceMethod, SourceMethod>();
            var queue = new Queue<SourceMethod>();
            queue.Enqueue(from);
            parent[from] = null;
            while (queue.Count > 0)
            {
                SourceMethod current = queue.Dequeue();
                if (stop(current))
                    continue;
                if (goal(current))
                {
                    var path = new List<SourceMethod>();
                    for (SourceMethod at = current; at != null; at = parent[at])
                        path.Insert(0, at);
                    return path;
                }
                foreach (SourceMethod next in Callees(current))
                {
                    if (!parent.ContainsKey(next))
                    {
                        parent[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }
            return null;
        }
    }
}
