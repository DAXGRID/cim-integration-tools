using System;
using System.Collections.Generic;
using System.Linq;

namespace DAX.CIM.PhysicalNetworkModel.Traversal.Extensions
{
    public static class ConductingEquipmentEx
    {
        public static IEnumerable<IdentifiedObject> Traverse(this IdentifiedObject start, CimContext context, Predicate<ConductingEquipment> ciCriteria, Predicate<ConnectivityNode> cnCriteria = null, bool includeEquipmentsWhereCriteriaIsFalse = false)
        {
            context = context;

            var traversal = new BasicTraversal(start);

            return traversal.DFS(context, ciCriteria, cnCriteria, includeEquipmentsWhereCriteriaIsFalse);
        }

        public static IEnumerable<IdentifiedObjectWithHopInfo> TraverseWithHopInfo(this IdentifiedObject start, CimContext context, Predicate<ConductingEquipment> ciCriteria, Predicate<ConnectivityNode> cnCriteria = null, bool includeEquipmentsWhereCriteriaIsFalse = false)
        {
            context = context;

            var traversal = new BasicTraversal(start);

            return traversal.DFSWithHopInfo(ciCriteria, cnCriteria, includeEquipmentsWhereCriteriaIsFalse, context);
        }

        public static bool IsOpen(this ConductingEquipment conductingEquipment)
        {
            return (conductingEquipment as Switch)?.normalOpen ?? false;
        }

        public static bool IsInsideSubstation(this IdentifiedObject identifiedObject, CimContext context)
        {
            context = context;

            if (identifiedObject is Equipment)
                return ((Equipment)identifiedObject).GetSubstation(context, false) != null;

            // If connectivity node, check if associated with something inside a substation
            if (identifiedObject is ConnectivityNode)
            {
                var neighbors = context.GetConnections(identifiedObject);

                if (neighbors.Count(n => n.ConductingEquipment.GetSubstation(context, false) != null) > 0)
                    return true;
            }

            if (identifiedObject is VoltageLevel)
            {
                var voltageLevel = (VoltageLevel)identifiedObject;

                return voltageLevel.EquipmentContainer1.Get(context).GetSubstation(context, false) != null;
            }

            if (identifiedObject is BayExt)
            {
                var bayExt = (BayExt)identifiedObject;

                return bayExt.VoltageLevel.Get(context).GetSubstation(context, false) != null;
            }

            if (identifiedObject is PowerTransformerEnd)
            {
                var ptEnd = (PowerTransformerEnd)identifiedObject;

                //return bayExt.VoltageLevel.Get(context).GetSubstation(false, context) != null;
                if (ptEnd.PowerTransformer != null && ptEnd.PowerTransformer.@ref != null)
                {
                    return context.GetObject<PowerTransformer>(ptEnd.PowerTransformer.@ref).GetSubstation(context, false) != null;
                }
            }

            return false;
        }

        public static bool IsInsideSubstation(this VoltageLevel voltageLevel, CimContext context)
        {
            context = context;

            return voltageLevel.GetSubstation(context, false) != null;
        }

        public static Substation GetSubstation(this EquipmentContainer equipmentContainer, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            if (equipmentContainer is Substation)
            {
                return (Substation)equipmentContainer;
            }

            if (equipmentContainer is VoltageLevel)
            {
                var voltageLevel = (VoltageLevel)equipmentContainer;

                return voltageLevel.EquipmentContainer1.Get(context).GetSubstation(context, throwIfNotFound);
            }

            if (equipmentContainer is BayExt)
            {
                var bayExt = (BayExt)equipmentContainer;

                return bayExt.VoltageLevel.Get(context).GetSubstation(context, throwIfNotFound);
            }

            if (!throwIfNotFound) return null;

            throw new ArgumentException($"Could not find SubStation from equipment container {equipmentContainer}");
        }

        public static Substation GetSubstation(this Equipment conductingEquipment, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            if (throwIfNotFound)
            {
                var equipmentContainer = conductingEquipment.EquipmentContainer.Get(context);
                return equipmentContainer.GetSubstation(context, throwIfNotFound);

            }
            else
            {
                try
                {
                    var equipmentContainer = conductingEquipment.EquipmentContainer.Get(context);
                    return equipmentContainer.GetSubstation(context, throwIfNotFound);
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public static Substation GetSubstation(this ConnectivityNode connectivityNode, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            var neighbors = context.GetConnections(connectivityNode);

            foreach (var n in neighbors)
            {
                if (n.ConductingEquipment.GetSubstation(context, false) != null)
                    return n.ConductingEquipment.GetSubstation(context, false);
            }

            return null;
        }

        public static Substation GetSubstation(this IdentifiedObject identifiedObject, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            if (identifiedObject is Equipment)
                return GetSubstation((Equipment)identifiedObject, context, throwIfNotFound);
            else if (identifiedObject is ConnectivityNode)
                return GetSubstation((ConnectivityNode)identifiedObject, context, throwIfNotFound);
            else if (identifiedObject is VoltageLevel)
                return context.GetObject<Substation>(((VoltageLevel)identifiedObject).EquipmentContainer1.@ref);
            else if (identifiedObject is Bay)
                return GetSubstation(context.GetObject<VoltageLevel>(((Bay)identifiedObject).VoltageLevel.@ref), context, throwIfNotFound);
            else if (identifiedObject is TransformerEnd)
            {
                var terminal = context.GetObject<Terminal>(((TransformerEnd)identifiedObject).Terminal.@ref);
                var pt = context.GetObject<PowerTransformer>(terminal.ConductingEquipment.@ref);
                return context.GetObject<Substation>(pt.EquipmentContainer.@ref);
            }

            return null;
        }

        public static List<ConductingEquipment> GetNeighborConductingEquipments(this ConductingEquipment conductingEquipment, CimContext context)
        {
            context = context;

            List<ConductingEquipment> result = new List<ConductingEquipment>();

            var eqConnections = context.GetConnections(conductingEquipment);
            foreach (var eqConn in eqConnections)
            {
                var cnConnections = context.GetConnections(eqConn.ConnectivityNode);

                foreach (var cnCon in cnConnections)
                {
                    if (cnCon.ConductingEquipment != conductingEquipment)
                        result.Add(cnCon.ConductingEquipment);
                }
            }

            return result;
        }

        public static List<ConductingEquipment> GetNeighborConductingEquipments(this ConnectivityNode cn, CimContext context)
        {
            context = context;

            List<ConductingEquipment> result = new List<ConductingEquipment>();

            var cnConnections = context.GetConnections(cn);

            foreach (var cnCon in cnConnections)
            {
                result.Add(cnCon.ConductingEquipment);
            }

            return result;
        }

        public static Bay GetBay(this IdentifiedObject identifiedObject, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            if (identifiedObject is Equipment)
            {
                var equipmentContainer = ((Equipment)identifiedObject).EquipmentContainer.Get(context);

                if (equipmentContainer != null && equipmentContainer is Bay)
                    return (Bay)equipmentContainer;
                else
                    return null;
            }
            else if (identifiedObject is ConnectivityNode)
            {
                var neighbors = context.GetConnections(identifiedObject);

                foreach (var n in neighbors)
                {
                    if (n.ConductingEquipment.GetBay(context) != null)
                        return n.ConductingEquipment.GetBay(context);
                }

                return null;
            }
                

            return null;
        }

        public static bool HasBay(this IdentifiedObject identifiedObject, CimContext context, bool throwIfNotFound = true)
        {
            return GetBay(identifiedObject, context, false) != null;
        }

        public static EquipmentContainer Get(this EquipmentEquipmentContainer equipmentEquipmentContainer, CimContext context)
        {
            if (equipmentEquipmentContainer == null)
                return null;

            context = context;

            return context.GetObject<EquipmentContainer>(equipmentEquipmentContainer.@ref);
        }

        public static EquipmentContainer Get(this VoltageLevelEquipmentContainer voltageLevelEquipmentContainer, CimContext context)
        {
            context = context;

            return context.GetObject<EquipmentContainer>(voltageLevelEquipmentContainer.@ref);
        }

        public static EquipmentContainer Get(this BayVoltageLevel bayVoltageLevel, CimContext context)
        {
            context = context;

            return context.GetObject<EquipmentContainer>(bayVoltageLevel.@ref);
        }

        /// <summary>
        /// Find the terminal that is connected to the conducting equipment specified in connectedTo
        /// </summary>
        /// <param name="conductingEquipment"></param>
        /// <param name="connectedTo"></param>
        /// <param name="throwIfNotFound"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Terminal GetTerminal(this ConductingEquipment conductingEquipment, ConductingEquipment connectedTo, CimContext context, bool throwIfNotFound = true)
        {
            context = context;

            var terminalConnections = context.GetConnections(conductingEquipment);
            
            foreach (var connection in terminalConnections)
            {
                if (connection.ConnectivityNode != null)
                {
                    var cnConnections = context.GetConnections(connection.ConnectivityNode);

                    // See if any of the CEs that the terminal's CN is connected to is equal connectedTo
                    foreach (var cnConnection in cnConnections)
                    {
                        if (cnConnection.ConductingEquipment == connectedTo)
                            return connection.Terminal;
                    }
                }
            }

            if (throwIfNotFound)
                throw new KeyNotFoundException("Cannot find any conduction equipment with mRID=" + connectedTo.mRID + " connected to conducting equipment with mRID=" + conductingEquipment.mRID);

            return null;
        }

    }
}
