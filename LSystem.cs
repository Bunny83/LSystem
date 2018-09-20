#region License and Information
/*****
* LSystem.cs
* 
* This class represents a generic parametric Lindenmayer system.
* (https://en.wikipedia.org/wiki/L-system)
* 
* This framework requires the LogicExpressionParser to parse module conditions
* and to evaluate the replacement rule parameters.
* 
* The LSystemParser can parse a simple text based format into an LSystem.
* Things in square brackets are optional.
* 
* Axiom: A[; B]
* [Count: X]
* Rules: A --> B; A; B
*       [B --> A; C; A]
*       [C --> C; B; C]
*       
* Each rule need to be on a seperate line. With "Count" you can specify an
* initial iteration count that will be carried out right away.
* 
* The rule string syntax looks like this:
* 
*    "Name[(P1, P2) [: condition]] --> [Name1[(P1, P2)] [;Name2[(P1, P2)]]]"
*    
* Name        - The name of the module that should be replaced by this rule
* P1, P2      - The parameters of the module. You can use any name for the
*               parameters. The parameter count is variable and depends on
*               the module.
* condition   - This is a logic expression that has to evaluate to "true",
*               otherwise this rule is ignored. The logic expression can use
*               any of the parameters and any variable that has been added
*               manually
* Name1,Name2 - The names of the replacement modules. The parameters of the
*               replacement modules can be a numerical expression involving
*               any parameters or custom variables.
* 
* 
* 
* [History]
* 2017.08.20 - first release version.
* 
* [License]
* Copyright (c) 2017 Markus Göbel (Bunny83)
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to
* deal in the Software without restriction, including without limitation the
* rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
* sell copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
* FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
* IN THE SOFTWARE.
* 
*****/
#endregion License and Information

using B83.LogicExpressionParser;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B83.LSystem.StringBased
{
    // represents a single "symbol" with optional parameters
    public class Module
    {
        public string Name;
        public List<double> parameters = null;
        public void Serialize(StringBuilder aSB)
        {
            aSB.Append(Name);
            if (parameters != null && parameters.Count > 0)
            {
                aSB.Append('(');
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (i > 0)
                        aSB.Append(',');
                    aSB.Append(parameters[i]);
                }
                aSB.Append(")");
            }
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }
    }

    // represents a replacement and is used in a replacement rule to define
    // a single replacement "symbol".
    public class ReplacementModule
    {
        public string Name;
        public List<NumberExpression> parameters = null;
        public Module Generate(Module aSource)
        {
            var m = new Module { Name = Name };
            if (parameters != null && parameters.Count > 0)
            {
                m.parameters = new List<double>(parameters.Count);
                foreach (var P in parameters)
                {
                    m.parameters.Add(P.GetNumber());
                }
            }
            return m;
        }
    }

    // represents a single replacement rule.
    public class ReplacementRule
    {
        public string Name;
        public List<string> parameterNames = new List<string>();
        public LogicExpression condition = null;
        public ExpressionContext variableContext = new ExpressionContext();
        public List<ReplacementModule> replacement = new List<ReplacementModule>();
        public bool Apply(Module aCurrent, List<Module> aNewString)
        {
            if (aCurrent == null)
                return false;
            if (aCurrent.Name != Name)
                return false;
            if (parameterNames != null && parameterNames.Count > 0 && parameterNames.Count != aCurrent.parameters.Count)
                return false;
            if (parameterNames != null && aCurrent.parameters != null && parameterNames.Count > 0)
                for (int i = 0; i < parameterNames.Count; i++)
                    variableContext[parameterNames[i]].Set(aCurrent.parameters[i]);
            if (condition != null && !condition.GetResult())
                return false;
            for (int i = 0; i < replacement.Count; i++)
            {
                aNewString.Add(replacement[i].Generate(aCurrent));
            }
            return true;
        }
    }

    public class LSystem
    {
        public List<Module> symbols = new List<Module>();
        public List<ReplacementRule> rules = new List<ReplacementRule>();
        public void Iterate(List<Module> aInput, List<Module> aOutput)
        {
            if (aInput == null || aOutput == null)
                return;
            foreach (var m in aInput)
            {
                bool noReplacement = true;
                foreach (var rule in rules)
                {
                    if (rule.Apply(m, aOutput))
                    {
                        noReplacement = false;
                        break;
                    }
                }
                if (noReplacement)
                    aOutput.Add(m);
            }
        }
        public void Iterate(int aCount = 1)
        {
            var l = new List<Module>();
            var tmp = l;
            for (int i = 0; i < aCount; i++)
            {
                Iterate(symbols, tmp);
                l = symbols;
                symbols = tmp;
                tmp = l;
                l.Clear();
            }
        }
        public override string ToString()
        {
            return symbols.Select(m => m.ToString()).Aggregate((a, b) => a + "; " + b);
        }
    }

    public class LSystemParser
    {
        private static string[] m_ReplacementOperator = new string[] { "-->" };
        public Parser parser = new Parser();

        public List<Module> ParseAxiom(string aText)
        {
            List<Module> result = new List<Module>();
            foreach (var r in aText.Split(';'))
            {
                int open = r.IndexOf('(');
                var m = new Module();
                if (open > 0)
                {
                    m.Name = r.Substring(0, open).Trim();
                    int close = ParsingContext.FindClosingBracket(r, open, '(', ')');
                    m.parameters = new List<double>();
                    foreach (var p in r.Substring(open + 1, close - open - 1).Split(','))
                    {
                        var num = parser.ParseNumber(p);
                        if (num == null)
                            throw new ParseException("Can't parse the parameter '" + p + "' of the axiom");
                        m.parameters.Add(num.GetNumber());
                    }
                }
                else
                    m.Name = r.Trim();
                result.Add(m);
            }
            return result;
        }

        // Examples
        // "A --> B; A; C"
        // "Fib(a, b): a < 2000 --> Fib(a+b,a)"
        // "Name(P1, P2) : condition --> Name1(P1, P2); Name2(P1, P2)"
        public ReplacementRule ParseRule(string aText)
        {
            var result = new ReplacementRule();
            var parts = aText.Split(m_ReplacementOperator, System.StringSplitOptions.None);
            if (parts.Length < 2)
                throw new ParsingException("Rule syntax error. No '-->' operator found");
            if (parts.Length > 2)
                throw new ParsingException("Rule syntax error. Too many '-->' " +
                    "operators found. There should be only one per rule");
            var leftParts = parts[0].Split(':');
            string name = leftParts[0];
            int pos = name.IndexOf('(');
            if (pos > 0)
            {
                int close = ParsingContext.FindClosingBracket(name, pos, '(', ')');
                string parameterStr = name.Substring(pos + 1, close - pos - 1);
                name = name.Substring(0, pos);
                result.parameterNames = new List<string>(parameterStr.Split(',').Select(p => p.Trim()));
                if (leftParts.Length > 1)
                    result.condition = parser.Parse(leftParts[1], result.variableContext);
            }
            result.Name = name.Trim();
            foreach (var r in parts[1].Split(';'))
            {
                int open = r.IndexOf('(');
                var rm = new ReplacementModule();
                if (open > 0)
                {
                    rm.parameters = new List<NumberExpression>();
                    rm.Name = r.Substring(0, open).Trim();
                    int close = ParsingContext.FindClosingBracket(r, open, '(', ')');
                    foreach (var p in r.Substring(open + 1, close - open - 1).Split(','))
                    {
                        var num = parser.ParseNumber(p, result.variableContext);
                        if (num == null)
                            throw new ParseException("Can't parse '" + p + "' into a rule expression parameter");
                        rm.parameters.Add(num);
                    }
                }
                else
                    rm.Name = r.Trim();
                if (rm.Name != "")
                    result.replacement.Add(rm);
            }
            return result;
        }

        public LSystem ParseSystem(string aText)
        {
            var reader = new System.IO.StringReader(aText);
            var result = new LSystem();
            int count = -1;
            bool parseRules = false;
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                line = line.Trim();
                var ll = line.ToLower();
                if (ll.StartsWith("axiom:"))
                    result.symbols = ParseAxiom(line.Substring(6));
                else if (ll.StartsWith("count:"))
                    int.TryParse(line.Substring(6), out count);
                else if (ll.StartsWith("rules:"))
                {
                    parseRules = true;
                    line = line.Substring(6).Trim();
                }
                if (parseRules && line.Length > 4)
                    result.rules.Add(ParseRule(line));
            }
            if (count > 0)
            {
                result.Iterate(count);
            }
            return result;
        }

        public class ParsingException : System.Exception
        {
            public ParsingException(string aMessage) : base(aMessage) { }
        }
    }
}


