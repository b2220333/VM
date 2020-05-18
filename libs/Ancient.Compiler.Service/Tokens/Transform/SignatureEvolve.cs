﻿namespace ancient.compiler.tokens
{
    using System.Collections.Generic;
    using System.Linq;
    using runtime;
    using runtime.emit.sys;

    public class SignatureEvolve : ClassicEvolve
    {
        internal readonly List<string> _argumentTypes;
        internal readonly string _signatureName;

        public SignatureEvolve(List<string> argumentTypes, string signatureName)
        {
            _argumentTypes = argumentTypes;
            _signatureName = signatureName;
        }


        protected override void OnBuild(List<Instruction> jar)
        {
            jar.AddRange(new Instruction[]
            {
                new sig(_signatureName, _argumentTypes.Count),
                new lpstr(_signatureName), 
                new raw(0),
                new orb((byte)_argumentTypes.Count)
            });
            jar.AddRange(_argumentTypes.Select(x => new raw(ExternType.FindAndConstruct(x))));
        }
    }
}