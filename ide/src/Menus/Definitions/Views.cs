﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MOAI.Menus.Definitions.Views
{
    class Code : Action
    {
        public Code() : base() { }
        public Code(object context) : base(context) { }

        public override void OnInitialize()
        {
            this.Implemented = false;
            this.ItemIcon = Properties.Resources.view_code;
            this.Text = "Code";
            this.Enabled = false;
            this.MenuItem.Checked = true;
        }
    }

    class Designer : Action
    {
        public Designer() : base() { }
        public Designer(object context) : base(context) { }

        public override void OnInitialize()
        {
            this.Implemented = false;
            this.ItemIcon = Properties.Resources.view_designer;
            this.Text = "Designer";
            this.Enabled = false;
            this.MenuItem.Checked = false;
        }
    }
}
