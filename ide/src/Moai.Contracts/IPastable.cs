﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Moai
{
    public interface IPastable
    {
        bool CanPaste { get; }

        void Paste();
    }
}
