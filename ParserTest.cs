using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace MyTests
{
    public interface IExpressionVisitor
    {
        void Visit(Literal expression);
        void Visit(Variable expression);
        void Visit(BinaryExpression expression);
        void Visit(ParenExpression expression);
    }
    
    public interface IExpression
    {
        void Accept(IExpressionVisitor visitor);
    }

    public class Literal : IExpression
    {
        public Literal(char value)
        {
            Value = value;
        }

        public readonly char Value;
        
        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public class Variable : IExpression
    {
        public Variable(char name)
        {
            Name = name;
        }

        public readonly char Name;
        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
    
    public class BinaryExpression : IExpression
    {
        public readonly IExpression FirstOperand;
        public readonly IExpression SecondOperand;
        public readonly char Operator;

        public BinaryExpression(IExpression firstOperand, IExpression secondOperand, char @operator)
        {
            FirstOperand = firstOperand;
            SecondOperand = secondOperand;
            Operator = @operator;
        }

        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
    
    public class ParenExpression : IExpression
    {
        public ParenExpression(IExpression operand)
        {
            Operand = operand;
        }

        public readonly IExpression Operand;
        public void Accept(IExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }

    public class DumpVisitor : IExpressionVisitor
    {
        private readonly StringBuilder myBuilder;

        public DumpVisitor()
        {
            myBuilder = new StringBuilder();
        }

        public void Visit(Literal expression)
        {
            myBuilder.Append("Literal(" + expression.Value + ")");
        }

        public void Visit(Variable expression)
        {
            myBuilder.Append("Variable(" + expression.Name + ")");
        }

        public void Visit(BinaryExpression expression)
        {
            myBuilder.Append("Binary(");
            expression.FirstOperand.Accept(this);
            myBuilder.Append(expression.Operator);
            expression.SecondOperand.Accept(this);
            myBuilder.Append(")");
        }

        public void Visit(ParenExpression expression)
        {
            myBuilder.Append("Paren(");
            expression.Operand.Accept(this);
            myBuilder.Append(")");
        }

        public override string ToString()
        {
            return myBuilder.ToString();
        }
    }
    
    
    public static class SimpleParser
    {

        public static IExpression Parse(string text)
        {
            text += '@'; // end of text
            Stack<IExpression> expressions = new Stack<IExpression>();
            Stack<char> operations = new Stack<char>();

            Dictionary<char, int> prior = new Dictionary<char, int>();
            prior['+'] = 1;
            prior['-'] = 1;
            prior['*'] = 2;
            prior['/'] = 2;
            prior['@'] = -1;
            prior[')'] = 0;
            prior['('] = 10;

            var i = 0;
            while (i < text.Length)
            {
                var ch = text[i];
                
                if (operations.Any() && operations.Peek() == '@') break;
                
                if (ch == ')' && operations.Peek() == '(')
                {
                    operations.Pop();
                    expressions.Push(new ParenExpression(expressions.Pop()));
                    i++;
                    continue;
                } 
                
                if (prior.Keys.Contains(ch))
                {
                    if (!operations.Any() || prior[operations.Peek()] < prior[ch] || operations.Peek() == '(')
                    {
                        operations.Push(ch);
                        i++;
                    }
                    else
                    {
                        IExpression rightOperand = expressions.Pop();
                        IExpression leftOperand = expressions.Pop();

                        char operation = operations.Pop();
                        expressions.Push(new BinaryExpression(leftOperand,
                            rightOperand, operation));
                    }
                }
                else if (char.IsDigit(ch))
                {
                    expressions.Push(new Literal(ch));
                    i++;
                }
                else if (char.IsLetter(ch))
                {
                    expressions.Push(new Variable(ch));
                    i++;
                }
            }

            return expressions.Pop();
        }
    }


    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("1+2").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Literal(1)+Literal(2))", dumpVisitor.ToString());

            Assert.Pass();
        }

        [Test]
        public void Test2()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("5-2").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Literal(5)-Literal(2))", dumpVisitor.ToString());

            Assert.Pass();
        }

        [Test]
        public void Test3()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("5*0").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Literal(5)*Literal(0))", dumpVisitor.ToString());

            Assert.Pass();
        }

        [Test]
        public void Test4()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("s/0").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Variable(s)/Literal(0))", dumpVisitor.ToString());

            Assert.Pass();
        }

        [Test]
        public void Test5()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("s+4+d-t").Accept(dumpVisitor);
            Console.WriteLine(dumpVisitor.ToString());
            Assert.AreEqual("Binary(Binary(Binary(Variable(s)+Literal(4))+Variable(d))-Variable(t))",
                dumpVisitor.ToString());

            Assert.Pass();
        }
        
        [Test]
        public void Test6()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("s+4*d-t").Accept(dumpVisitor);
            Console.WriteLine(dumpVisitor.ToString());
            Assert.AreEqual("Binary(Binary(Variable(s)+Binary(Literal(4)*Variable(d)))-Variable(t))",
                dumpVisitor.ToString());

            Assert.Pass();
        }
        [Test]
        public void Test7()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("(1+2)*4").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Paren(Binary(Literal(1)+Literal(2)))*Literal(4))", dumpVisitor.ToString());

            Assert.Pass();
        }
        
        [Test]
        public void Test8()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("1+2*4").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Literal(1)+Binary(Literal(2)*Literal(4)))", dumpVisitor.ToString());

            Assert.Pass();
        }
        
        [Test]
        public void Test9()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("1+(2*4)").Accept(dumpVisitor);
            Assert.AreEqual("Binary(Literal(1)+Paren(Binary(Literal(2)*Literal(4))))", dumpVisitor.ToString());

            Assert.Pass();
        }
        
        [Test]
        public void Test10()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("d+(f+5)*8-(4+(f-3)/3)").Accept(dumpVisitor);
            
            Assert.AreEqual("Binary(Binary(Variable(d)+Binary(Paren(Binary(Variable(f)+Literal(5)))*Literal(8)))-Paren(Binary(Literal(4)+Binary(Paren(Binary(Variable(f)-Literal(3)))/Literal(3)))))", dumpVisitor.ToString());

            Assert.Pass();
        }

        [Test]
        public void Test11()
        {
            var dumpVisitor = new DumpVisitor();
            SimpleParser.Parse("(3)").Accept(dumpVisitor);
            Assert.AreEqual("Paren(Literal(3))", dumpVisitor.ToString());

            Assert.Pass();
        }
    }
}