﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIM.PhysicalNetworkModel
{
    public partial class PowerTransformerInfoExt : PowerTransformerInfo
    {

        private PU lowerBoundField;

        private ApparentPower thermalRatedSField;

        private PU upperBoundField;
     
        private bool hasInternalDeltaWindingField;   

        /// <remarks/>
        public PU lowerBound
        {
            get
            {
                return this.lowerBoundField;
            }
            set
            {
                this.lowerBoundField = value;
            }
        }

        /// <remarks/>
        public ApparentPower thermalRatedS
        {
            get
            {
                return this.thermalRatedSField;
            }
            set
            {
                this.thermalRatedSField = value;
            }
        }

        /// <remarks/>
        public PU upperBound
        {
            get
            {
                return this.upperBoundField;
            }
            set
            {
                this.upperBoundField = value;
            }
        }
             
        /// <remarks/>
        public bool hasInternalDeltaWinding
        {
            get
            {
                return this.hasInternalDeltaWindingField;
            }
            set
            {
                this.hasInternalDeltaWindingField = value;
            }
        }


    }

}
