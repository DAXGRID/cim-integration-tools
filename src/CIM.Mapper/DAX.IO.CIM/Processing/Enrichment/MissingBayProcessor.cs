using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{

    public class MissingBayProcessor : IGraphProcessor
    {
        public void Initialize(string name, List<ConfigParameter> parameters = null)
        {
        }

        public void Run(CIMGraph g, CimErrorLogger tableLogger)
        {
            Logger.Log(LogLevel.Debug, "MissingBayProcessor: Add missing bays for PSI to be happy...");
            FixCableOnHorn(g);
            FixCableOnCableOnHorn(g);
            AddMissignBayOnFlyingSwitches(g);
        }

        private static void FixCableOnCableOnHorn(CIMGraph g)
        {
            foreach (var sharedCN in g.CIMObjects)
            {
               if (sharedCN.ClassType == CIMClassEnum.ConnectivityNode || sharedCN.ClassType == CIMClassEnum.ConnectivityEdge)
               {
                    var trafos = sharedCN.GetNeighbours(CIMClassEnum.PowerTransformer);
                    var cables = sharedCN.GetNeighbours(CIMClassEnum.ACLineSegment);

                    if (cables.Count == 2)
                    {
                        CIMIdentifiedObject trafoCable = null;
                        CIMIdentifiedObject ingoingCable = null;
                        CIMEquipmentContainer substation = null;
                        CIMConductingEquipment trafo = null;

                        if (cables[0].EquipmentContainerRef == null
                            && cables[1].EquipmentContainerRef != null)
                        {
                            substation = cables[1].EquipmentContainerRef;
                            trafoCable = cables[1];
                            ingoingCable = cables[0];
                        }

                        if (cables[1].EquipmentContainerRef == null
                            && cables[0].EquipmentContainerRef != null)
                        {
                            substation = cables[0].EquipmentContainerRef;
                            trafoCable = cables[0];
                            ingoingCable = cables[1];
                        }

                        if (trafoCable != null)
                        {
                            var trafoCableTransformers = trafoCable.GetNeighboursNeighbors(CIMClassEnum.PowerTransformer);
                            if (trafoCableTransformers != null && trafoCableTransformers.Count == 1)
                                trafo = trafoCableTransformers[0] as CIMConductingEquipment;
                        }

                        // cable on cable on horn
                        if (substation != null && trafo != null && trafoCable != null && ingoingCable != null)
                        {
                            // Create fictional bay
                            CIMEquipmentContainer bay = new CIMEquipmentContainer(g.ObjectManager) { ClassType = CIMClassEnum.Bay };
                            bay.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 1);
                            bay.Name = "Auto generated";
                            bay.Description = null;
                            bay.VoltageLevel = ingoingCable.VoltageLevel;

                            // Add bay to substation
                            substation.Children.Add(bay);
                            bay.EquipmentContainerRef = substation;

                            // Create disconnecting link (disconnector)
                            CIMConductingEquipment fiktivSwitch = new CIMConductingEquipment(g.ObjectManager) { ClassType = CIMClassEnum.Disconnector };
                            fiktivSwitch.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 5);
                            fiktivSwitch.Name = "Auto generated disconnecting link";
                            fiktivSwitch.VoltageLevel = ingoingCable.VoltageLevel;
                            fiktivSwitch.SetPSRType(CIMMetaDataManager.Repository, "DisconnectingLink");
                            
                            // Add disconneting link to bay
                            bay.Children.Add(fiktivSwitch);
                            fiktivSwitch.EquipmentContainerRef = bay;

                            // Disconnect shared cn from ingoing cable
                            sharedCN.RemoveNeighbour(ingoingCable);
                            ingoingCable.RemoveNeighbour(sharedCN);

                            // Connect shared cn to switch
                            sharedCN.AddNeighbour(fiktivSwitch);
                            fiktivSwitch.AddNeighbour(sharedCN);

                            // Create extra CN (to be used between switch and incoming cable)
                            var extraCn = new CIMConnectivityNode(g.ObjectManager);
                            extraCn.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 10);
                            extraCn.EquipmentContainerRef = bay;

                            // Connect switch to extra CN
                            extraCn.AddNeighbour(fiktivSwitch);
                            fiktivSwitch.AddNeighbour(extraCn);

                            // Connect extra CN to incomming cable
                            extraCn.AddNeighbour(ingoingCable);
                            ingoingCable.AddNeighbour(extraCn);

                            /////////////////////////////////////
                            // ADD DAX transformer feeder

                            if (trafoCable.VoltageLevel > 1000)
                                g.AddFeeder(new CreateFeederInfo() { ConnectivityNode = extraCn, IsTransformerFeeder = true, Transformer = trafo }, extraCn);

                            System.Diagnostics.Trace.WriteLine("PSI HACK 1: " + substation.Name + " " + substation.Description + " " + trafoCable.VoltageLevel);
                        }


                    }

                
                }
            }
        }

        private static void FixCableOnHorn(CIMGraph g)
        {
            foreach (var sharedCN in g.CIMObjects)
            {
                if (sharedCN.ClassType == CIMClassEnum.ConnectivityNode)
                {
                    var trafos = sharedCN.GetNeighbours(CIMClassEnum.PowerTransformer);
                    var cables = sharedCN.GetNeighbours(CIMClassEnum.ACLineSegment);

                    if (trafos.Count == 1 && cables.Count > 0)
                    {
                        CIMIdentifiedObject ingoingCable = null;
                        CIMEquipmentContainer substation = trafos[0].EquipmentContainerRef as CIMEquipmentContainer;
                        CIMConductingEquipment trafo = trafos[0] as CIMConductingEquipment;

                        foreach (var cable in cables)
                        {
                            // Er det et rigtig kabel?
                            if (cable.EquipmentContainerRef == null)
                                ingoingCable = cable;
                        }

                        // cable direct on horn
                        if (substation != null && trafo != null && ingoingCable != null)
                        {
                            // Create fictional bay
                            CIMEquipmentContainer bay = new CIMEquipmentContainer(g.ObjectManager) { ClassType = CIMClassEnum.Bay };
                            bay.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 1);
                            bay.Name = "Auto generated";
                            bay.Description = null;
                            bay.VoltageLevel = ingoingCable.VoltageLevel;

                            // Add bay to substation
                            substation.Children.Add(bay);
                            bay.EquipmentContainerRef = substation;

                            // Create disconnecting link (disconnector)
                            CIMConductingEquipment fiktivSwitch = new CIMConductingEquipment(g.ObjectManager) { ClassType = CIMClassEnum.Disconnector };
                            fiktivSwitch.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 5);
                            fiktivSwitch.Name = "Auto generated disconnecting link";
                            fiktivSwitch.VoltageLevel = ingoingCable.VoltageLevel;
                            fiktivSwitch.SetPSRType(CIMMetaDataManager.Repository, "DisconnectingLink");

                            // Add disconneting link to bay
                            bay.Children.Add(fiktivSwitch);
                            fiktivSwitch.EquipmentContainerRef = bay;

                            // Disconnect shared cn from ingoing cable
                            sharedCN.RemoveNeighbour(ingoingCable);
                            ingoingCable.RemoveNeighbour(sharedCN);

                            // Connect shared cn to switch
                            sharedCN.AddNeighbour(fiktivSwitch);
                            fiktivSwitch.AddNeighbour(sharedCN);

                            // Create extra CN (to be used between switch and incoming cable)
                            var extraCn = new CIMConnectivityNode(g.ObjectManager);
                            extraCn.mRID = GUIDHelper.CreateDerivedGuid(sharedCN.mRID, 10);
                            extraCn.EquipmentContainerRef = bay;

                            // Connect switch to extra CN
                            extraCn.AddNeighbour(fiktivSwitch);
                            fiktivSwitch.AddNeighbour(extraCn);

                            // Connect extra CN to incomming cable
                            extraCn.AddNeighbour(ingoingCable);
                            ingoingCable.AddNeighbour(extraCn);

                            /////////////////////////////////////
                            // ADD DAX transformer feeder

                            if (ingoingCable.VoltageLevel > 1000)
                                g.AddFeeder(new CreateFeederInfo() { ConnectivityNode = extraCn, IsTransformerFeeder = true, Transformer = trafo }, extraCn);

                            System.Diagnostics.Trace.WriteLine("PSI HACK 2: " + substation.Name + " " + substation.Description + " " + ingoingCable.VoltageLevel);
                        }


                    }


                }
            }
        }

        private static void AddMissignBayOnFlyingSwitches(CIMGraph g)
        {
            // Trace all AC Line segments
            foreach (var obj in g.CIMObjects)
            {
                // If disconnector, loadbreakswith, breaker or fuse sits directly inside substation, we have to create a fictional bay
                if (obj.EquipmentContainerRef != null &&
                    (obj.EquipmentContainerRef.ClassType == CIMClassEnum.Substation ||
                    obj.EquipmentContainerRef.ClassType == CIMClassEnum.Enclosure) &&
                    (obj.ClassType == CIMClassEnum.Disconnector ||
                    obj.ClassType == CIMClassEnum.LoadBreakSwitch ||
                    obj.ClassType == CIMClassEnum.Breaker ||
                    obj.ClassType == CIMClassEnum.Fuse))
                {
                    // Create fictional bay
                    CIMEquipmentContainer bay = new CIMEquipmentContainer(g.ObjectManager) { ClassType = CIMClassEnum.Bay };
                    bay.EquipmentContainerRef = obj.EquipmentContainerRef;
                    bay.mRID = GUIDHelper.CreateDerivedGuid(obj.mRID, 5);

                    if (obj.ContainsPropertyValue("bay.name"))
                        bay.Name = obj.GetPropertyValueAsString("bay.name");
                    else
                        bay.Name = "Auto generated";

                    bay.Description = obj.Name;
                    bay.VoltageLevel = obj.VoltageLevel;
                    obj.EquipmentContainerRef.Children.Add(bay);

                    // Add component to bay
                    bay.Children.Add(obj);

                    // Remove switch for old container
                    obj.EquipmentContainerRef.Children.Remove(obj);

                    // Set switch to new container
                    obj.EquipmentContainerRef = bay;
                }
            }
        }
    }
}
