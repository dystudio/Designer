﻿namespace FormsDesigner.Commands
{
    using System.ComponentModel.Design;

    public class HorizSpaceIncrease : AbstractFormsDesignerCommand
    {
        public override System.ComponentModel.Design.CommandID CommandID
        {
            get
            {
                return StandardCommands.HorizSpaceIncrease;
            }
        }
    }
}

