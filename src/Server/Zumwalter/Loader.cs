﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RowAC
{
    public class Loader
    {
        public static void Init()
        {
            UnityEngine.Debug.Log("[RowAC] loading...");
            RowAnticheat.Init();
        }
    }
}