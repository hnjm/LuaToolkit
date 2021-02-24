﻿using System;
using System.Collections.Generic;
using System.Text;
using LuaSharpVM.Core;
using LuaSharpVM.Models;
using LuaSharpVM.Disassembler;

namespace LuaSharpVM.Decompiler
{
    public class LuaScriptLine
    {
        public int Depth;
        private int number;
        public int Number // Line number OR index?
        {
            set
            {
                number = value;
                NumberEnd = value; // + 1;
            }
            get { return number; }
        }

        public int NumberEnd; // in case we need more

        private OpcodeType OpType;

        private LuaDecoder Decoder;
        private LuaFunction Func;
        public LuaInstruction Instr;
        public List<int> BranchInc = new List<int>();

        public string Op1; // opperands ;D
        public string Op2;
        public string Op3;

        private string _text;
        public string Text
        {
            get { if (_text == null) { return ToString(); } else { return _text; }; }
            set { _text = value; }
        }

        public LuaScriptLine(string wildcard)
        {
            this.Op1 = wildcard;
        }

        public LuaScriptLine(LuaInstruction instr, ref LuaDecoder decoder, ref LuaFunction func)
        {
            this.Instr = instr;
            this.Func = func;
            this.Decoder = decoder;
            SetType();
            SetMain();
        }

        private void SetMain()
        {
            switch (this.Instr.OpCode)
            {
                case LuaOpcode.MOVE:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = WriteIndex(Instr.B);
                    break;
                case LuaOpcode.LOADK:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = GetConstant(Instr.Bx);
                    break;
                case LuaOpcode.LOADBOOL:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = Instr.B != 0 ? "true" : "false";
                    break;
                case LuaOpcode.LOADNIL:
                    for (int i = Instr.A; i < Instr.B + 1; ++i)
                    {
                        this.Op2 += $"{WriteIndex(i)} = nil; "; // NOTE: wont conflict with 'local' keyword?

                        //this.Op2 += $"{WriteIndex(i)}"; // NOTE: keep it clean? (TODO: figure out 'local' keyword)
                        //if (i < Instr.B - 1)
                        //    this.Op2 += ", "; // inline
                    }
                    break;
                case LuaOpcode.GETUPVAL:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = this.Func.Upvalues[Instr.B].ToString().Substring(1, this.Func.Upvalues[Instr.B].ToString().Length - 2); // this is legit for prototypes etc
                    break;
                case LuaOpcode.GETGLOBAL:
                    this.Op1 = $"{WriteIndex(Instr.A)} = _G[";
                    this.Op2 = GetConstant(Instr.Bx); // may be used lateron
                    this.Op3 = $"]";
                    break;
                case LuaOpcode.GETTABLE:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = $"var{Instr.B}[{WriteIndex(Instr.C)}]"; // TODO: fix???
                    break;
                case LuaOpcode.SETGLOBAL:
                    this.Op1 = $"_G[{GetConstant(Instr.Bx)}]";
                    this.Op2 = " = ";
                    this.Op3 = $"var{Instr.A}";
                    break;
                case LuaOpcode.SETUPVAL:
                    //this.Op1 = $"upvalue[{WriteIndex(Instr.B)}]"; TODO: Verify if below actually work
                    this.Op1 = this.Func.Upvalues[Instr.B].ToString().Substring(1, this.Func.Upvalues[Instr.B].ToString().Length - 2);
                    this.Op2 = " = ";
                    this.Op3 = $"var[{GetConstant(Instr.A)}]";
                    break;
                case LuaOpcode.SETTABLE:
                    this.Op1 = $"{WriteIndex(Instr.A)}[{WriteIndex(Instr.B)}]";
                    this.Op2 = " = ";
                    this.Op3 = WriteIndex(Instr.C);
                    break;
                case LuaOpcode.NEWTABLE:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = "{}";
                    break;
                case LuaOpcode.SELF:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = " = ";
                    this.Op3 = $"var{Instr.B}; {WriteIndex(Instr.A)} = var{Instr.B}[{WriteIndex(Instr.C)}]";
                    // TODO fix, multiline?
                    this.NumberEnd++;
                    break;
                case LuaOpcode.ADD: // NOTE these can be both variables and constants!
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    this.Op3 = $" + {WriteConstant(Instr.C)}";
                    break;
                case LuaOpcode.SUB:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    this.Op3 = $" - {WriteConstant(Instr.C)}";
                    break;
                case LuaOpcode.MUL:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    this.Op3 = $" * {WriteConstant(Instr.C)}";
                    break;
                case LuaOpcode.DIV:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    this.Op3 = $" / {WriteConstant(Instr.C)}";
                    break;
                case LuaOpcode.MOD:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    this.Op3 = $" % {WriteConstant(Instr.C)}";
                    break;
                case LuaOpcode.POW:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = {WriteConstant(Instr.B)}";
                    //this.Op3 = $" ^ var{Instr.C}"; // not always?
                    this.Op3 = $" ^ {WriteConstant(Instr.C)}"; // not always?
                    break;
                case LuaOpcode.UNM:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = ";
                    this.Op3 = $"-var{Instr.B}";
                    break;
                case LuaOpcode.NOT:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = ";
                    this.Op3 = $"not var{Instr.B}";
                    break;
                case LuaOpcode.LEN:
                    this.Op1 = WriteIndex(Instr.A);
                    this.Op2 = $" = ";
                    this.Op3 = $"#var{Instr.B}";
                    break;
                case LuaOpcode.CONCAT:
                    this.Op1 = $"{WriteIndex(Instr.A)} = ";
                    for (int i = Instr.B; i <= Instr.C; ++i)
                    {
                        this.Op2 += $"{WriteIndex(i)}";
                        if (i < Instr.C)
                            this.Op2 += " .. ";
                    }
                    break;
                case LuaOpcode.JMP:
                    // Do nothing ;D?
                    //this.Op3 = $"JMP {Instr.sBx}"; // NOTE: uncomment for debugging
                    break;
                case LuaOpcode.EQ:
                    this.Op1 = $"if";
                    //this.Op2 = $" ({WriteIndex(Instr.B)} == {WriteIndex(Instr.C)}) ~= {Instr.A} "; // A = and/or
                    if (Instr.A == 0)
                        this.Op2 = $" {WriteIndex(Instr.B)} == {WriteIndex(Instr.C)} ";
                    else
                        this.Op2 = $" {WriteIndex(Instr.B)} ~= {WriteIndex(Instr.C)} ";
                    this.Op3 = $"then";
                    break;
                case LuaOpcode.LT:
                    this.Op1 = $"if";
                    //this.Op2 = $"({WriteIndex(Instr.B)} < {WriteIndex(Instr.C)}) ~= {Instr.A} ";
                    if(Instr.A == 0)
                        this.Op2 = $" {WriteIndex(Instr.B)} < {WriteIndex(Instr.C)} ";
                    else
                        this.Op2 = $" {WriteIndex(Instr.B)} > {WriteIndex(Instr.C)} ";
                    this.Op3 = $"then";
                    break;
                case LuaOpcode.LE:
                    this.Op1 = $"if";
                    //this.Op2 = $"  ({WriteIndex(Instr.B)} <= {WriteIndex(Instr.C)}) ~= {Instr.A} ";
                    if (Instr.A == 0)
                        this.Op2 = $" {WriteIndex(Instr.B)} <= {WriteIndex(Instr.C)} ";
                    else
                        this.Op2 = $" {WriteIndex(Instr.B)} > {WriteIndex(Instr.C)} ";
                    this.Op3 = $"then";
                    break;
                case LuaOpcode.TEST:
                    this.Op1 = $"if";
                    this.Op2 = $" not var{Instr.A} ~= {Instr.C} ";
                    this.Op3 = $"then";
                    break;
                case LuaOpcode.TESTSET:
                    this.Op1 = $"if var{Instr.B} ~= {Instr.C} then; ";
                    this.Op2 = $"var{Instr.A} = ";
                    this.Op3 = $"var{Instr.B}; end";
                    break;
                case LuaOpcode.CALL:
                    // Function returns
                    if(Instr.C == 0)
                    {
                        // top set to last_result+1
                    }
                    else if(Instr.C == 1)  
                    {
                        // no return values saved
                    }
                    else // 2 or more, multiple returns
                    {
                        //for (int i = Instr.A; i < Instr.A + Instr.C-1; ++i)
                        for (int i = Instr.A; i < Instr.A + Instr.C-1; i++)
                        {
                            this.Op1 += $"var{i}";
                            if (i < Instr.A + Instr.C - 2)
                                this.Op1 += ", ";
                        }
                        this.Op1 += " = ";
                    }

                    // Function Name
                    this.Op2 = $"var{Instr.A}"; // func name only (used lateron)
                    
                    // Function Args
                    if(Instr.B == 0)
                    {
                        // func parms range from A+1 to B (B = top of stack)
                        this.Op3 = "(";
                        for (int i = Instr.A; i < Instr.B; i++)
                        //for (int i = Instr.A; i < Instr.A + Instr.B - 1; ++i)
                        {
                            this.Op3 += $"var{i + 1}";
                            if (i < Instr.A + Instr.B - 2)
                                this.Op3 += ", ";
                        }
                        this.Op3 += ")";
                    }
                    else 
                    {
                        this.Op3 = "(";
                        for (int i = Instr.A; i < Instr.A + Instr.B - 1; i++)
                        //for (int i = Instr.A; i < Instr.A + Instr.B - 1; ++i)
                        {
                            this.Op3 += $"var{i + 1}";
                            if (i < Instr.A + Instr.B - 2)
                                this.Op3 += ", ";
                        }
                        this.Op3 += ")";
                    }
                    break;
                // TAILCALL
                case LuaOpcode.RETURN:
                    // this gets overwritten by an 'end' afterwards in case its the last RETURN value of a func
                    this.Op1 = $"return";
                    if (Instr.B == 1)
                        break; // no arguments

                    else if (Instr.B > 1)
                    {
                        for (int j = 0; j < Instr.B - 1; j++)
                        {
                            this.Op2 += $" {WriteIndex(Instr.A + j)}"; // args from A to A+(B-2)
                            if (j < Instr.B - 2)
                                this.Op2 += ",";
                        }
                    }
                    else
                    {
                        for (int j = Instr.A; j < this.Func.MaxStackSize; j++)
                        {
                            this.Op2 += $"{WriteIndex(Instr.A + j)}"; // args from A to top
                            if (j < this.Func.MaxStackSize - 1)
                                this.Op2 += ",";
                        }
                    }
                    break;
                case LuaOpcode.FORLOOP:
                    //this.Op1 = "end"; // performs a negative jump to start of loop based on condition
                    break;
                case LuaOpcode.FORPREP:
                    // A+0: i =
                    // A+1: max 
                    // A+2: +=
                    // A+3: external index
                    this.Op1 = $"for {WriteIndex(Instr.A + 3, false)}={WriteIndex(Instr.A)}, {WriteIndex(Instr.A + 1)}, {WriteIndex(Instr.A + 2)} do";
                    break;
                case LuaOpcode.TFORLOOP:
                    this.Op1 = WriteIndex(Instr.A + 1) + ", " + WriteIndex(Instr.A + 2); // state
                    for(int i = Instr.A+3; i <= Instr.A+2+Instr.C; i++) // local loop variable result, A+3 up to A+2+C
                    {
                        this.Op2 += WriteIndex(i);
                        if (i < Instr.A + 2 + Instr.C)
                            this.Op2 += ", ";
                    }
                    this.Op3 = WriteIndex(Instr.A); // iterator func
                    break;
                case LuaOpcode.SETLIST:
                    this.Op1 = $"{WriteIndex(Instr.A)} = {{";
                    for (int i = 1; i <= Instr.B; i++)
                    {
                        this.Op2 += $"{WriteIndex(Instr.A + i)}";
                        if (i < Instr.B)
                            this.Op2 += ", ";
                    }
                    this.Op3 = "}";
                    break;
                case LuaOpcode.CLOSE:
                    // NOTE: close all variables in the stack up to
                    // has no impact on the decompiler?
                    break;
                case LuaOpcode.CLOSURE:
                    this.Op1 = $"{WriteIndex(Instr.A)}";
                    this.Op2 = " = ";
                    this.Op3 = $"{WriteIndex(Instr.Bx)}";
                    break;
                case LuaOpcode.VARARG:
                    // TODO: this.Op1 = "..."; // B > 1 for fixed range, B 0 for unspecified
                    break;
                default:
                    this.Op1 = "unk";
                    this.Op2 = "_";
                    this.Op3 = Instr.OpCode.ToString();
                    break;
            }
        }

        private void SetType()
        {
            switch (this.Instr.OpCode)
            {
                case LuaOpcode.LOADK:
                case LuaOpcode.GETGLOBAL:
                case LuaOpcode.SETGLOBAL:
                case LuaOpcode.CLOSURE:
                    this.OpType = OpcodeType.ABx;
                    break;
                case LuaOpcode.FORLOOP:
                case LuaOpcode.FORPREP:
                case LuaOpcode.JMP:
                    this.OpType = OpcodeType.AsBx;
                    break;
                default:
                    this.OpType = OpcodeType.ABC;
                    break;
            }
        }

        private string GetConstant(int index)
        {
            if (index >= this.Func.Constants.Count)
                return "\"unk" + index.ToString() + "\""; // indicates incorrect behavior

            return this.Func.Constants[index].ToString();
        }

        private string WriteConstant(int index, LuaFunction targetFunc = null)
        {
            if (targetFunc == null)
                targetFunc = this.Func; // self
            if (index > 255 && targetFunc.Constants[index - 256] != null)
                return targetFunc.Constants[index - 256].ToString();
            else
                return WriteIndex(index);
        }


        // NOTE: use this on LuaScriptFunction.GetConstant ??
        public string WriteIndex(int value, bool useLocalKeyword = true)
        {
            bool constant = false;
            int index = ToIndex(value, out constant);

            if (constant)
                return this.Func.Constants[index].ToString();
            else
            {
                // TODO: check if local and not yet used!
                if(this.Func.ScriptFunction.UsedLocals.Contains(value))
                    return "var" + index;
                else
                {
                    this.Func.ScriptFunction.UsedLocals.Add(value);
                    if (useLocalKeyword)
                        return "local var" + index;
                    else
                        return "var" + index;
                }
            }
        }

        private int ToIndex(int value, out bool isConstant)
        {
            // this is the logic from lua's source code (lopcodes.h)
            if (isConstant = (value & 1 << 8) != 0)
                return value & ~(1 << 8);
            else
                return value;
        }

        public override string ToString()
        {
            // TODO: leave tab to another level?
            string tab = new string('\t', Depth); // NOTE: singple space for debugging
            if (this.Instr == null)
                return $"{tab}{Op1}\r\n"; // wildcard
            else if ((this.Op1 == "" || this.Op1  == null)
                && (this.Op2 == "" || this.Op2 == null)
                &&  (this.Op3 == "" || this.Op3 == null))
                return "\r\n";
            else
                return $"{tab}{Op1}{Op2}{Op3}\r\n";
        }

        public bool IsCondition()
        {
            switch(this.Instr.OpCode)
            {
                case LuaOpcode.LE:
                case LuaOpcode.LT:
                case LuaOpcode.EQ:
                ////case LuaOpcode.TEST:
                ////case LuaOpcode.TESTSET:
                //case LuaOpcode.LOADBOOL: // untested
                    return true;
            }
            return false;
        }

        public bool IsBranch()
        {
            switch(this.Instr.OpCode)
            {
                // those change PC
                case LuaOpcode.JMP:
                case LuaOpcode.FORLOOP:
                case LuaOpcode.TFORLOOP:
                    return true;
            }
            return false;
        }

        public bool IsMove()
        {
            switch (this.Instr.OpCode)
            {
                case LuaOpcode.MOVE: // anything else, sir?
                    return true;
            }
            return false;
        }
    }
}
