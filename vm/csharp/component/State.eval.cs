namespace vm.component
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using ancient.runtime;
    using ancient.runtime.@base;
    using ancient.runtime.emit.sys;
    using ancient.runtime.emit.@unsafe;
    using ancient.runtime.exceptions;
    using ancient.runtime.hardware;
    using ancient.runtime.@unsafe;

    using static System.MathF;
    using Module = ancient.runtime.emit.sys.Module;

    public partial class State : IState
    {
        public void Eval()
        {
            MemoryManagement.FastWrite = fw;
            if (bf)
            {
                bus.debugger.handleBreak(u16 & pc, this);
                mem[0x17] = 0x0;
            }

            switch (iid)
            {
                case 0x0:
                    trace("call :: skip");
                    break;

                case { } opcode when opcode.In(0xD0..0xE8):
                    {
                        /* need @float-flag */
                        if (!ff) bus.cpu.halt(0xA9);
                        trace($"call :: [0xD0..0xE8]::0x{iid:X} [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                        var result = iid switch
                        { // todo refactoring
                            0xD0 => f32u64 & Abs(u64f32 & pipe[0x1]),
                            0xD1 => f32u64 & Acos(u64f32 & pipe[0x1]),
                            0xD2 => f32u64 & Atan(u64f32 & pipe[0x1]),
                            0xD3 => f32u64 & Acosh(u64f32 & pipe[0x1]),
                            0xD4 => f32u64 & Atanh(u64f32 & pipe[0x1]),
                            0xD5 => f32u64 & Asin(u64f32 & pipe[0x1]),
                            0xD6 => f32u64 & Asinh(u64f32 & pipe[0x1]),
                            0xD7 => f32u64 & Cbrt(u64f32 & pipe[0x1]),
                            0xD8 => f32u64 & Ceiling(u64f32 & pipe[0x1]),
                            0xD9 => f32u64 & Cos(u64f32 & pipe[0x1]),
                            0xDA => f32u64 & Cosh(u64f32 & pipe[0x1]),

                            0xDB => f32u64 & Floor(u64f32 & pipe[0x1]),
                            0xDC => f32u64 & Exp(u64f32 & pipe[0x1]),
                            0xDD => f32u64 & Log(u64f32 & pipe[0x1]),
                            0xDE => f32u64 & Log10(u64f32 & pipe[0x1]),
                            0xDF => f32u64 & Tan(u64f32 & pipe[0x1]),
                            0xE0 => f32u64 & Tanh(u64f32 & pipe[0x1]),

                            0xE4 => f32u64 & Atan2(u64f32 & pipe[0x1], u64f32 & pipe[0x2]),
                            0xE5 => f32u64 & Min(u64f32 & pipe[0x1], u64f32 & pipe[0x2]),
                            0xE6 => f32u64 & Max(u64f32 & pipe[0x1], u64f32 & pipe[0x2]),

                            0xE7 => f32u64 & Sin(u64f32 & pipe[0x1]),
                            0xE8 => f32u64 & Sinh(u64f32 & pipe[0x1]),

                            0xE1 => f32u64 & Truncate(u64f32 & pipe[0x1]),
                            0xE2 => f32u64 & BitDecrement(u64f32 & pipe[0x1]),
                            0xE3 => f32u64 & BitIncrement(u64f32 & pipe[0x1]),
                            _ => throw new CorruptedMemoryException("")
                        };
                        pipe[0x3] = result;
                        break;
                    }
                #region halt

                case 0xF when new[] { r1, r2, r3, u1, u2, x1 }.All(x => x == 0xF):
                    bus.cpu.halt(0xF);
                    break;

                case 0xD when r1 == 0xE && r2 == 0xA && r3 == 0xD:
                    bus.cpu.halt(0x0);
                    break;

                case 0xB when r1 == 0x0 && r2 == 0x0 && r3 == 0xB && u1 == 0x5:
                    bus.cpu.halt(0x1);
                    break;

                #endregion halt

                case 0x1 when x2 == 0x0:
                    trace($"call :: ldi 0x{u1:X}, 0x{u2:X} -> 0x{r1:X}");
                    _ = u2 switch
                    {
                        0x0 => mem[r1] = u1,
                        _ => mem[r1] = i64 | ((u2 << 4) | u1)
                    };
                    break;

                case 0x1 when x2 == 0xA:
                    trace($"call :: ldx 0x{u1:X}, 0x{u2:X} -> 0x{r1:X}-0x{r2:X}");
                    mem[((r1 << 4) | r2)] = i64 | ((u1 << 4) | u2);
                    break;

                case 0x3: /* @swap */
                    trace($"call :: swap, 0x{r1:X}, 0x{r2:X}");
                    mem[r1] ^= mem[r2];
                    mem[r2] = mem[r1] ^ mem[r2];
                    mem[r1] ^= mem[r2];
                    break;

                case 0xF when x2 == 0xE: // 0xF9988E0
                    trace($"call :: move, dev[0x{r1:X}] -> 0x{r2:X} -> 0x{u1:X}");
                    bus.find(r1 & 0xFF).write(r2 & 0xFF, i32 & mem[u1] & 0xFF);
                    break;

                case 0xF when x2 == 0xC: // 0xF00000C
                    trace($"call :: move, dev[0x{r1:X}] -> 0x{r2:X} -> [0x{u1:X}-0x{u2:X}]");
                    bus.find(r1 & 0xFF).write(r2 & 0xFF, (r3 << 12 | u1 << 8 | u2 << 4 | x1) & 0xFFFFFFF);
                    break;

                case 0xA3: /* wtd */
                    trace($"call :: wtd dev[0x{r1:X}{r2:X}] -> 0x{u1:X}{u2:X}");
                    bus.find((d8u)(u8 & r1, u8 & r2)).write((d8u)(u8 & u1, u8 & u2), (d8u)(u8 & x1, u8 & x2));
                    stack.push(bus.find(r1 & 0xFF).read(r2 & 0xFF));
                    break;

                case 0xA4: /* @rfd */
                    trace($"call :: rfd dev[0x{r1:X}{r2:X}] -> 0x{u1:X}{u2:X}");
                    stack.push(bus.find((d8u)(u8 & r1, u8 & r2)).read((d8u)(u8 & u1, u8 & u2)));
                    break;

                case 0x8 when u2 == 0xC: /* @ref.t */
                    trace($"call :: ref.t 0x{r1:X}");
                    mem[r1] = pc;
                    break;

                case 0xA0:
                    trace($"call :: orb '{r1}' times");
                    for (var i = pc + r1; pc != i;)
                        stack.push(fetch());
                    break;

                case 0xA1:
                    trace($"call :: pull -> 0x{r1:X}");
                    mem[r1] = stack.pop();
                    break;

                case 0xA5: /* @sig */
                    d8u arg_count = (u8 & r1, u8 & r2);
                    var returnType = ExternType.Find(
                        (u8 & r3, u8 & u1),
                        (u8 & u2, u8 & x1),
                        (u8 & x2, u8 & x3),
                        (u8 & x4, u8 & o1)
                    );
                    // read function name
                    var lp = (fetch() & 0x0000_0000_FFFF_FFFF_0000) >> 12 >> 4;
                    var p = StringLiteralMap.GetInternedString((int) lp);
                    NativeString.Unwrap(p,
                        out var functionName, false, true);

                    // read argument declaration
                    var args = new Utb[arg_count];
                    for (var i = 0; i != arg_count; i++)
                        args[i] = (ExternType.FindAndConstruct(fetch()), 0);

                    var memory = bus.find(0x0) as Memory;

                    Debug.Assert(memory != null, $"{nameof(memory)} != null");

                    // write function into memory
                    var (free, startPoint) = memory.GetFreeAddress();

                    memory.writeString(ref free, Module.Current.Name);
                    memory.writeString(ref free, functionName);
                    
                    memory.write(free++, arg_count);
                    foreach (var arg in args)
                    {
                        memory.writeString(ref free, arg.ConstructType().ShortName);
                        memory.write(free++, arg.Value);
                    }
                    memory.writeString(ref free, returnType.ShortName);


                    var bodyStart = free;

                    var n_frag = fetch();
                    while (AcceptOpCode(n_frag) != 0xA6 /* @ret */)
                    {
                        memory.write(free++, n_frag);
                        n_frag = fetch();
                    }
                    memory.write(free++, n_frag);

                    VMRef metadataRef = (startPoint, free - startPoint);
                    VMRef bodyRef = (bodyStart, free - bodyStart);

                    trace($"call :: declare function [{functionName}() -> {returnType.ShortName}] ");

                    Module.Current.RegisterFunction(new Function((metadataRef, bodyRef), memory));
                    break;
                case 0xA6: /* @ret */
                    pc = cr.Recoil();
                    CallStack.Exit();
                    trace("call :: ret");
                    break; 
                case 0x36: /* @call.i */
                    cr.Branch(pc);
                    d32i fp = (
                        u8 & r1, u8 & r2,
                        u8 & r3, u8 & u1,
                        u8 & u2, u8 & x1,
                        u8 & x2, u8 & x3
                    );
                    var f = Module.Current.FindFunction(fp);
                    trace($"call :: call.i !{{{f.Name}}}() -> {f.ReturnType.ShortName}");
                    CallStack.Enter(f, pc);
                    pc = f.GetCoilRef().Point;
                    break;
                case 0x41:
                    d32i mn = (
                        u8 & r1, u8 & r2,
                        u8 & r3, u8 & u1,
                        u8 & u2, u8 & x1,
                        u8 & x2, u8 & x3
                    );
                    NativeString.Unwrap(StringLiteralMap.GetInternedString(mn),
                        out var modulePath, false, true);
                    trace($"call :: use '{modulePath}'");
                    Module.Import(modulePath);
                    break;
                case 0xB1: /* @inc */
                    trace($"call :: increment [0x{r1:X}]++");
                    if(!ff)
                        unchecked { mem[r1]++; }
                    else
                        mem[r1] = f32u64 & ((u64f32 & mem[r1]) + 1f);
                    break;

                case 0xB2:  /* @dec */
                    trace($"call :: decrement [0x{r1:X}]--");
                    if (!ff)
                        unchecked { mem[r1]--; }
                    else
                        mem[r1] = f32u64 & ((u64f32 & mem[r1]) - 1f);
                    break;

                case 0xB3: /* @dup */
                    trace($"call :: dup 0x{(u1 << 4) | u2:X}");
                    mem[(u1 << 4) | u2] = mem[(r1 << 4) | r2];
                    break;

                case 0xB4: /* @ckft */
                    trace($"call :: ckft 0x{(r1 << 4) | r2:X}");
                    if (ff && !float.IsFinite(u64f32 & mem[(r1 << 4) | r2]))
                        bus.cpu.halt(0xA9);
                    break;
                case 0x40: /* @__static_extern_call */
                    d16u sign = (
                        u8 & r1, u8 & r2, 
                        u8 & r3, u8 & u1
                    );
                    trace($"call :: static_call 0x{sign.Value:X}");
                    var find = Module.Context.Find(sign, out var @extern);
                    if (find != ExternStatus.Found)
                    {
                        bus.cpu.halt(0xA16 + (int)find, $"0x{sign.Value:X}");
                        return;
                    }
                    trace($"call :: {@extern.Signature}");
                    CallAndWrite(@extern);
                    break;

                case 0x37: /* @prune */
                    evaluation.Prune();
                    break;

                case 0x38: /* @locals */
                    d8u len = (u8 & r1, u8 & r2);
                    evaluation.Alloc(len);
                    var segs = new List<EvaluationSegment>(len);
                    for (var i = 0; i != len; i++)
                        segs.Add(EvaluationSegment.Construct(fetch(), fetch()));
                    var @params = new object[len];
                    foreach (var s in segs)
                        @params[s.Index] = ExternType.Find(s.Type);
                    locals.Pin(@params, out var @external);
                    foreach (var (host, e_index) in @external.Select((x, i) => (x, i)))
                        evaluation[e_index] = host;
                    break;

                case 0x39: /* @readonly */
                    break;

                /*
                case 0xB5: / @ixor /
                case 0xB6: / @ior /

                    d8u first = (u8 & r1, u8 & r2);
                    d8u second = (u8 & u1, u8 & u2);
                    // not support float-mode
                    if (ff) bus.cpu.halt(0xA9);
                    if (iid == 0xB5)
                        mem[first] ^= mem[second];
                    if (iid == 0xB6)
                        mem[first] |= mem[second];
                    break;
                */
                case 0xB5: /* @neg */
                    d8u c1 = (u8 & r1, u8 & r2);
                    trace($"call :: neg [0x{(ushort)c1:X}]");
                    if (ff)
                        mem[c1] = f32u64 & ((u64f32 & mem[c1]) * -1f);
                    else
                        mem[c1] = (ulong)((long)mem[c1] * -1);
                    break;

                case 0x34: /* @lpstr */
                    d32u str_index = (u8 & r1, u8 & r2, u8 & r3, u8 & u1, u8 & u2, u8 & x1, u8 & x2, u8 & x3);
                    if (!StringLiteralMap.Has(str_index))
                        bus.cpu.halt(0xDE3, $"index {str_index.Value} not found in memory");
                    stack.push(str_index.Value);
                    break;

                case 0x35: /* @unlock */
                    d8u unlock_cell = (u8 & r1, u8 & r2);
                    mem[unlock_cell] = stack.pop();
                    mem_types[unlock_cell] = ExternType.Find(
                        (u8 & r3, u8 & u1),
                        (u8 & u2, u8 & x1),
                        (u8 & x2, u8 & x3)
                    );
                    break;

                case 0xC2: /* @dif_t */
                case 0xC3: /* @dif_f */
                    d8u target = (u8 & r1, u8 & r2);
                    trace($"call :: dif [0x{target:X}]");
                    if (!(mem_types[target] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{target:X}] is {mem_types[pipe.arg1].GetType().Name}");
                    d8u skip = (u8 & r3, u8 & u1);
                    if (mem[target] == (iid == 0xC2ul ? 0x1ul : 0x0ul))
                        pc += skip;
                    break;
                case 0xC5: /* @ceq */
                    trace($"call :: ceq [0x{pipe.arg1:X}] [0x{pipe.arg2:X}]");
                    pipe[0x3] = pipe[0x1] == pipe[0x2] ? 1ul : 0ul;
                    if(x3 == 0x1 || x3 == 0x3)
                        mem_types[pipe.result] = new i2_Type();
                    break;
                case 0xC6: /* @neq */
                    trace($"call :: neq [0x{pipe.arg1:X}] [0x{pipe.arg2:X}]");
                    pipe[0x3] = pipe[0x1] != pipe[0x2] ? 1ul : 0ul;
                    if (x3 == 0x1 || x3 == 0x3)
                        mem_types[pipe.result] = new i2_Type();
                    break;
                case 0xC7: /* @xor */
                    trace($"call :: xor [0x{pipe.arg1:X}] [0x{pipe.arg2:X}]");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg1:X}] is {mem_types[pipe.arg1].GetType().Name}");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg2:X}] is {mem_types[pipe.arg2].GetType().Name}");

                    pipe[0x3] = (pipe[0x1] == 0x1) ^ (pipe[0x2] == 0x1) ? 0x1ul : 0x0ul;
                    if (x3 == 0x1 || x3 == 0x3)
                        mem_types[pipe.result] = new i2_Type();
                    break;
                case 0xC8: /* @or */
                    trace($"call :: xor [0x{pipe.arg1:X}] [0x{pipe.arg2:X}]");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg1:X}] is {mem_types[pipe.arg1].GetType().Name}");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg2:X}] is {mem_types[pipe.arg2].GetType().Name}");

                    pipe[0x3] = (pipe[0x1] == 0x1) | (pipe[0x2] == 0x1) ? 0x1ul : 0x0ul;
                    if (x3 == 0x1 || x3 == 0x3)
                        mem_types[pipe.result] = new i2_Type();
                    break;
                case 0xC9: /* @and */
                    trace($"call :: xor [0x{pipe.arg1:X}] [0x{pipe.arg2:X}]");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg1:X}] is {mem_types[pipe.arg1].GetType().Name}");
                    if (!(mem_types[pipe.arg1] is i2_Type))
                        bus.cpu.halt(0xA22, $"[0x{pipe.arg2:X}] is {mem_types[pipe.arg2].GetType().Name}");

                    pipe[0x3] = (pipe[0x1] == 0x1) & (pipe[0x2] == 0x1) ? 0x1ul : 0x0ul;
                    if (x3 == 0x1 || x3 == 0x3)
                        mem_types[pipe.result] = new i2_Type();
                    break;
                #region debug

                case 0xF when x2 == 0xF: /* @mvx */
                    string toString(ushort memAddr)
                    {
                        var value = mem[memAddr];
                        var type = mem_types[memAddr];
                        if (type is Unknown_Type)
                            return ff ?
                                (u64f32 & value).ToString(CultureInfo.InvariantCulture) :
                                value.ToString(CultureInfo.InvariantCulture);
                        if (type is str_Type)
                        {
                            var p = StringLiteralMap.GetInternedString((int)value);
                            NativeString.Unwrap(p, out var result_str, true, true);
                            return result_str ?? "<null>";
                        }
                        return $"<{type.GetType().Name.Replace("_Type", "").ToLowerInvariant()}>";
                    }
                    foreach (var uuu in toString(u1).Select(x => (int)x))
                        bus.find(r1 & 0xFF).write(r2 & 0xFF, uuu);
                    break;

                case 0xC1 when x2 == 0x1: /* @break :: now */
                    trace($"[0x{iid:X}] @break :: now");
                    bus.debugger.handleBreak(u16 & pc, this);
                    mem[0x17] = 0x0;
                    break;

                case 0xC1 when x2 == 0x2: /* @break :: next */
                    trace($"[0x{iid:X}] @break :: next");
                    mem[0x17] = 0x1;
                    break;

                case 0xC1 when x2 == 0x3: /* @break :: after */
                    trace($"[0x{iid:X}] @break :: after");
                    mem[0x17] = 0x3;
                    break;

                #endregion debug

                #region jumps

                case 0x8 when u2 == 0xF && x1 == 0x0: /* @jump_t */
                    trace($"jump_t 0x{r1:X}");
                    warn("jump_t has obsolete");
                    pc = mem[r1];
                    break;

                case 0x8 when u2 == 0xF && x1 == 0x1: /* @jump_e  0x8FCD0F10 */
                    trace(mem[r2] >= mem[r3]
                        ? $"jump_e 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> apl"
                        : $"jump_e 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> skip");
                    warn("jump_e has obsolete");
                    if (mem[r2] >= mem[r3])
                        pc = mem[r1];
                    break;

                case 0x8 when u2 == 0xF && x1 == 0x2: /* @jump_g */
                    trace(mem[r2] > mem[r3]
                        ? $"jump_g 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> apl"
                        : $"jump_g 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> skip");
                    warn("jump_g has obsolete");
                    if (mem[r2] > mem[r3])
                        pc = mem[r1];
                    break;

                case 0x8 when u2 == 0xF && x1 == 0x3: /* @jump_u */
                    trace(mem[r2] < mem[r3]
                        ? $"jump_u 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> apl"
                        : $"jump_u 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> skip");
                    warn("jump_u has obsolete");
                    if (mem[r2] < mem[r3])
                        pc = mem[r1];
                    break;

                case 0x8 when u2 == 0xF && x1 == 0x4: /* @jump_y */
                    trace(mem[r2] <= mem[r3]
                        ? $"jump_y 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> apl"
                        : $"jump_y 0x{r1:X} -> 0x{r2:X} 0x{r3:X} -> skip");
                    warn("jump_y has obsolete");
                    if (mem[r2] <= mem[r3]) pc = mem[r1];
                    break;

                case 0x09 when x3 == 0x1: /* @jump_p */
                    pc = mem[(d8u)(u8 & r1, u8 & r2)];
                    trace($"jump_p [0x{r1:X}{r2:X}] -> apl");
                    break;
                case 0x09 when x3 == 0x2: /* @jump_x */
                    if (mem_types[(d8u) (u8 & r1, u8 & r2)] is u2_Type && mem[(d8u) (u8 & r1, u8 & r2)] == 0x1)
                    {
                        pc = mem[(d8u)(u8 & r3, u8 & u1)];
                        trace($"jump_x [0x{r1:X}{r2:X}] -> true -> apl");
                        break;
                    }
                    trace($"jump_x [0x{r1:X}{r2:X}] -> false -> skip");
                    break;
                #endregion jumps

                #region math

                case 0xCA:
                    trace($"call :: add [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                    if (ff)
                        pipe[0x3] = f32u64 & (u64f32 & pipe[0x1]) + (u64f32 & pipe[0x2]);
                    else
                        pipe[0x3] = pipe[0x1] + pipe[0x2];
                    break;

                case 0xCB:
                    trace($"call :: sub [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                    if (ff)
                        pipe[0x3] = f32u64 & (u64f32 & pipe[0x1]) - (u64f32 & pipe[0x2]);
                    else
                        pipe[0x3] = pipe[0x1] - pipe[0x2];
                    break;

                case 0xCC:
                    trace($"call :: div [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                    _ = (pipe[0x2], ff) switch
                    {
                        (0x0, _) => (ulong)bus.cpu.halt(0xC),
                        (_, false) => pipe[0x3] = pipe[0x1] / pipe[0x2],
                        (_, true) => pipe[0x3] = f32u64 & (u64f32 & pipe[0x1]) / (u64f32 & pipe[0x2])
                    };
                    break;

                case 0xCD:
                    trace($"call :: mul [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                    if (ff)
                        pipe[0x3] = f32u64 & (u64f32 & pipe[0x1]) * (u64f32 & pipe[0x2]);
                    else
                        pipe[0x3] = pipe[0x1] * pipe[0x2];
                    break;

                case 0xCE:
                    trace($"call :: pow [0x{pipe.arg1:X}] [0x{pipe.arg2:X}] with 0x{x3:X} mode");
                    if (ff)
                        pipe[0x3] = f32u64 & Pow((u64f32 & pipe[0x1]), (u64f32 & pipe[0x2]));
                    else
                        pipe[0x3] = (ulong)Pow(pipe[0x1], pipe[0x2]);
                    break;

                case 0xCF:
                    trace($"call :: sqrt 0x{r2:X}");
                    if (ff)
                        pipe[0x3] = f32u64 & Sqrt((u64f32 & pipe[0x1]));
                    else
                        pipe[0x3] = (ulong)Sqrt(pipe[0x1]);
                    break;

                #endregion math
                default:
                    bus.cpu.halt(0xFC);
                    Error($"call :: unknown opCode -> {iid:X2}");
                    break;
            }
            /* @break :: after */
            if (mem[0x17] == 0x3) mem[0x17] = 0x2;
            if (mem[0x17] == 0x2)
            {
                bus.debugger.handleBreak(u16 & pc, this);
                mem[0x17] = 0x0;
            }
            /* @break :: end */
            IncrementClockStep();
        }

        public class Evaluate
        {
            private Stack<object> stack;
            private List<object> table;

            public object this[int index]
            {
                get => table[index];
                set => table[index] = value;
            }

            public void Prune()
            {
                stack?.Clear();
                table?.Clear();
                stack = null;
                table = null;
            }

            public void Alloc(int len)
            {
                stack = new Stack<object>(len);
                table = new List<object>(len);
            }
        }

        public readonly Evaluate evaluation = new Evaluate();

        private void CallAndWrite(ExternSignature info)
        {
            if (info.IsArgs())
            {
                bus.cpu.halt(0xA22, "not implemented");
                return;
            }
            try
            {
                var result = info.method.Invoke(null, new object[0]);
                if (!info.IsVoid())
                    stack.push(result.To<ulong>());
            }
            catch (Exception e)
            {
                bus.cpu.halt(0xA22, e.Message);
            }
        }
        #region Clock tracking

        private int clockStep { get; set; }
        private DateTimeOffset StartPoint { get; set; }
        public float GetHertz()
        {
            var sec = (float)(DateTimeOffset.UtcNow - StartPoint).TotalSeconds;
            if (sec >= 1) return 0;
            return Round(clockStep / sec, 2);
        }
        private void IncrementClockStep()
        {
            if (StartPoint == default)
                StartPoint = DateTimeOffset.UtcNow;
            if ((DateTimeOffset.UtcNow - StartPoint).TotalSeconds >= 1)
            {
                StartPoint = DateTimeOffset.UtcNow;
                clockStep = 0;
            }
            clockStep++;
        }
        #endregion
    }
}