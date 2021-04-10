using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MyTests
{
    public enum LexemeType
    {
        Symbol,
        Identifier,
        Comment,
        Number,
        CharacterString
    }
    public class Lexeme
    {
        public readonly LexemeType Type;
        public int Begin;
        public int End;
        public readonly List<Lexeme> Lexemes;  // for nested comments

        public Lexeme(LexemeType type, int begin, int end, List<Lexeme> lexemes=null)
        {
            Type = type;
            Begin = begin;
            End = end;
            Lexemes = lexemes;
        }

        public override string ToString()
        {
            var view = Type + "[" + Begin + "," + End + "]";
            if (Lexemes == null || Lexemes.Count == 0) return view;
            view += ":";
            for (var i=0; i < Lexemes.Count-1;i++)
            {
                view += Lexemes[i] + ", ";
            }

            view += Lexemes[Lexemes.Count - 1] + ";";

            return view;
        }
    }

    public static class SimpleLexer
    {
        private static int HexNum(string text)
        {
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9'};
            char[] alphas = {'a', 'b', 'c', 'd', 'e', 'f'};

            int current = 0;
            while (true)
            {
                if (current == text.Length || !digits.Contains(text[current]) && !alphas.Contains(text[current]))
                    return current;
                current++;
            }
        }

        private static int OctoNum(string text)
        {
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7'};

            int current = 0;
            while (true)
            {
                if (!digits.Contains(text[current]) || current == text.Length)
                    return current;
                current++;
            }
        }

        private static int BinNum(string text)
        {
            char[] digits = {'0', '1'};

            int current = 0;
            while (true)
            {
                if (current == text.Length || !digits.Contains(text[current]))
                    return current;
                current++;
            }
        }

        private static int DecNum(string text)
        {
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9'};
            int current = 0;

            // real part
            while (current < text.Length && digits.Contains(text[current]))
            {
                current++;
            }

            // если есть дробная часть
            if (current < text.Length && text[current].Equals('.'))
            {
                current++;
                while (current < text.Length && digits.Contains(text[current]))
                {
                    current++;
                }
            }

            // если есть scale factor
            if (current < text.Length && (text[current].Equals('e') || text[current].Equals('E')))
            {
                // if scale factor has sign
                current++;
                if (text[current].Equals('+') || text[current].Equals('-')) current++;

                while (current < text.Length && digits.Contains(text[current]))
                {
                    current++;
                }
            }

            return current;
        }

        private static Lexeme LexemeUnsignNum(string text)
        {
            int len;
            switch (text[0])
            {
                case '&':
                    len = OctoNum(text.Substring(1));
                    if (len == 0) return null;
                    return new Lexeme(LexemeType.Number, 0, 1 + len);
                case '%':
                    len = BinNum(text.Substring(1));
                    if (len == 0) return null;
                    return new Lexeme(LexemeType.Number, 0, 1 + len);
                case '$':
                    len = HexNum(text.Substring(1));
                    if (len == 0) return null;
                    return new Lexeme(LexemeType.Number, 0, 1 + len);
                default:
                    len = DecNum(text);
                    if (len == 0) return null;
                    return new Lexeme(LexemeType.Number, 0, len);
            }
        }

        private static Lexeme LexemeNum(string text)
        {
            Lexeme lexeme;
            switch (text[0])
            {
                case '+':
                case '-':
                    lexeme = LexemeUnsignNum(text.Substring(1));
                    return new Lexeme(LexemeType.Number, 0, lexeme.End + 1);
                default:
                    lexeme = LexemeUnsignNum(text.Substring(0));
                    return new Lexeme(LexemeType.Number, 0, lexeme.End);
            }

        }

        private static Lexeme LexemeIdentifier(string text)
        {
            text = text.Substring(0, Math.Min(127, text.Length));
            char[] alphas =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
                'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '_'
            };
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9'};
            int current = 0;
            if (text.StartsWith("&")) current++;
            if (!alphas.Contains(text[current]))
                return null;
            while (current < text.Length && (alphas.Contains(text[current]) || digits.Contains(text[current])))
            {
                current++;
            }

            if (current == 0) return null;
            return new Lexeme(LexemeType.Identifier, 0, current);
        }

        private static Lexeme LexemeComment(string text)
        {
            string start;
            string end;

            if (text.StartsWith("//"))
            {
                start = "//";
                end = "";
                if (text.IndexOf('\n') != -1) text = text.Substring(0, text.IndexOf('\n'));
            }
            else if (text.StartsWith("(*"))
            {
                start = "(*";
                end = "*)";
            }
            else
            {
                start = "{";
                end = "}";
            }
            
            var current = start.Length;

            List<Lexeme> lexemes = new List<Lexeme>();
            while (current < text.Length)
            {
                // nested
                if (current < text.Length && (
                    text.Substring(current).StartsWith("//") ||
                    text.Substring(current).StartsWith("(*") ||
                    text.Substring(current).StartsWith("{")))
                {
                    var lexeme = LexemeComment(text.Substring(current));
                    lexemes.Add(new Lexeme(lexeme.Type, lexeme.Begin + current, lexeme.End + current, lexeme.Lexemes));
                    current += lexeme.End;
                }

                if (!start.Equals("//") && current < text.Length && text.Substring(current).StartsWith(end))
                    return new Lexeme(LexemeType.Comment, 0, current + end.Length, lexemes);
                
                current++;
            }

            if (start.Equals("//"))
            {
                return new Lexeme(LexemeType.Comment, 0, text.Length, lexemes);
            }
            return null;
        }

        private static int SymbolsInString(string text)
        {
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9'};
            int current = 0;
            while (current < text.Length)
            {
                if (!text[current].Equals('#')) return current;
                current++;
                if (!digits.Contains(text[current])) return -1000;
                while (current < text.Length && digits.Contains(text[current])) current++;
            }

            return current;
        }

        private static int QuotesInString(string text)
        {
            int current = 0;
            while (current < text.Length && !text[current].Equals('\'')) current++;
            if (current == text.Length) return -1000;
            return current;
        }

        private static Lexeme LexemeCharString(string text)
        {
            if (text.IndexOf('\n') != -1)
                text = text.Substring(0, text.IndexOf('\n'));

            if (text.IndexOf('\r') != -1)
                text = text.Substring(0, text.IndexOf('\r'));

            int current = 0;

            while (current < text.Length)
            {
                int next;
                if (text[current].Equals('\'')) next = QuotesInString(text.Substring(current + 1)) + 2;
                else if (text[current].Equals('#')) next = SymbolsInString(text.Substring(current));
                else return new Lexeme(LexemeType.CharacterString, 0, current);
                

                if (next < 0) return new Lexeme(LexemeType.CharacterString, 0, current);
                current += next;
            }

            return new Lexeme(LexemeType.CharacterString, 0, current);
        }

        private static Lexeme FindNextLexeme(string text)
        {
            Lexeme savedLexeme = null;
            char[] digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9'};
            char[] alphas =
            {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
                'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
            };
            char[] numbStart = {'%', '$', '+', '-'};

            // CharacterString
            if (text[0].Equals('\'') || text[0].Equals('#')) savedLexeme = LexemeCharString(text);

            // Comment
            else if (text.StartsWith("(*") || text.StartsWith("{") || text.StartsWith("//"))
                savedLexeme = LexemeComment(text);

            // Identifier
            else if (alphas.Contains(text[0]) || text[0].Equals('_') ||
                     text[0].Equals('&') && (text.Length > 1) &&
                     (alphas.Contains(text[1]) || text[1].Equals('_')))
                savedLexeme = LexemeIdentifier(text);

            // Number
            else if (digits.Contains(text[0]) || numbStart.Contains(text[0]) ||
                     (text.Length > 1) && text[0].Equals('&') && digits.Contains(text[1]))
                savedLexeme = LexemeNum(text);

            if (savedLexeme == null || savedLexeme.End == savedLexeme.Begin) return new Lexeme(LexemeType.Symbol, 0, 1);
            return savedLexeme;
        }

        public static List<Lexeme> SplitToLexeme(string text)
        {
            List<Lexeme> lexemes = new List<Lexeme>();
            string tmp = text.ToLower();

            while (true)
            {
                Lexeme nextLexema = FindNextLexeme(tmp);

                nextLexema.Begin += text.Length - tmp.Length;
                nextLexema.End += text.Length - tmp.Length;
                lexemes.Add(nextLexema);

                if (text.Length == nextLexema.End) return lexemes;

                tmp = tmp.Substring(nextLexema.End - nextLexema.Begin);
            }
        }
    }

    public class Tests
    {
        public string viewSplit(List<Lexeme> lexemes)
        {
            var view = "";
            foreach (var item in lexemes)
            {
                view += item.ToString();
            }

            return view;
        }

        [Test]
        public void Test1()
        {
            // ’ + - * / = < > [ ] . , ( ) : ^ @ { } $ # & % << >> ** <> >< <= >= := += -= *= /= (* *) (. .) //
            var text = "*";
            var split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Symbol[0,1]", viewSplit(split));
            
            text = "(*";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Symbol[0,1]Symbol[1,2]", viewSplit(split));
            
            text = "(*'";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Symbol[0,1]Symbol[1,2]Symbol[2,3]", viewSplit(split));
            
            text = "&(*'";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Symbol[0,1]Symbol[1,2]Symbol[2,3]Symbol[3,4]", viewSplit(split));
        }
        
        [Test]
        public void Test2()
        {
            // Identifier: starts with letter, _ or &, includes letters and digits, length from 1 to 127
            var text = "a";
            var split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,1]", viewSplit(split));
            
            text = "a__34";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,5]", viewSplit(split));
            
            text = "_____";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,5]", viewSplit(split));
            
            text = "&a__34";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,6]", viewSplit(split));
            
            text = "&a_=_34";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,3]Symbol[3,4]Identifier[4,7]", viewSplit(split));
            
            text = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            Console.Write(text.Length);
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Identifier[0,127]Identifier[127,128]", viewSplit(split));
        }
        
        [Test]
        public void Test3()
        {
            // Numbers: sign (or no) unsigned real (digits . digits eE sign digits) or integer (dec, bin, hex, oct)
            
            // integer
            var text = "2";
            var split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,1]", viewSplit(split));
            
            text = "$267af";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,6]", viewSplit(split));
            
            text = "-$267afk";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,7]Identifier[7,8]", viewSplit(split));
            
            text = "&267af";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,4]Identifier[4,6]", viewSplit(split));
            
            text = "%1001010";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,8]", viewSplit(split));        
            
            text = "267.01e8";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,8]", viewSplit(split)); 
            
            text = "+267.01E+8";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Number[0,10]", viewSplit(split)); 
        }
        
        [Test]
        public void Test4()
        {
            // zero or more, no \n or \r quotes or control stings
            var text = "''";
            var split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("CharacterString[0,2]", viewSplit(split));
        
            text = "'hello world!'";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("CharacterString[0,14]", viewSplit(split));
            
            text = "'hello world!\n'";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Symbol[0,1]Identifier[1,6]Symbol[6,7]Identifier[7,12]Symbol[12,13]Symbol[13,14]Symbol[14,15]", viewSplit(split));
            
            text = "#34#56";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("CharacterString[0,6]", viewSplit(split));
            
            text = "'hello'#34#56'world'";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("CharacterString[0,20]", viewSplit(split));
            
            text = "'hello'#34#56";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("CharacterString[0,13]", viewSplit(split));
        }
        
        [Test]
        public void Test5()
        {
            var text = "{}";
            var split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,2]", viewSplit(split));
            
            text = "(*comment 1*)";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,13]", viewSplit(split));
            
            text = "{comment 1\ncomment 2}";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,21]", viewSplit(split));
            
            text = "//comment 1";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,11]", viewSplit(split));
            
            text = "//comment 1\ncode 2";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,11]Symbol[11,12]Identifier[12,16]Symbol[16,17]Number[17,18]", viewSplit(split));
            
            text = "//comment 1 (*comment 2*)";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,25]:Comment[12,25];", viewSplit(split));
            
            text = "//comment 1 //comment";
            split = SimpleLexer.SplitToLexeme(text);
            Assert.AreEqual("Comment[0,21]:Comment[12,21];", viewSplit(split));
        }
    }
}
