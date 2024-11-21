using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.IO.CIM
{
    public enum GeneralErrors
    {
        noError = 0,
        DanglingACLineSegmentMin = 100,
        DanglingACLineSegment = 100,
        DanglingACLineSegmentDouble = 101,
        DanglingACLineSegmentCloseToBay = 102,
        DanglingACLineSegmentCloseToConsumer = 103,
        DanglingACLineSegmentCloseToOtherDangling = 104,
        DanglingACLineSegmentCloseToEnclosure = 105,
        UnsupportedACLineSegment = 106,
        DanglingACLineSegmentMax = 199,
        DanglingConnectivityEdgeMin = 200,
        DanglingConnectivityEdge = 200,
        DanglingConnectivityNotKompunder = 201,
        DanglingConnectivityEdgeOnBusbarSection = 202,
        DanglingConnectivityEdgeMax = 299,
        WrongNumberOfTerminals = 300,
        ComponentHasNoParent = 310,
        ComponentHasParentFarAway = 311,
        ComponentHasNoRootParent = 312,
        ComponentHasNoVoltageLevel = 320,
        ComponentOverlayAnotherComponent = 400,
        NameNotUnique = 500,
        PowerTransformerHasNoConnections = 700,
        PowerTransformerHasNoTerminals = 701,
        PowerTransformerPrimaryTerminalNotConnected = 702,
        PowerTransformerExpectedOnePrimaryCable = 703,
        PowerTransformerExpectedPrimaryBusbar = 704,
        PowerTransformerPrimaryCableWrongVoltageLevel = 705,
        PowerTransformerPrimaryCableConnectedDirectlyToBusbar = 706,
        PowerTransformerHasNoConnectionToPrimarySide = 800,
        PowerTransformerHasNoConnectionToSecoundarySide = 900,
        AuxEquipmentCannotFindParent = 751,
        AuxEquipmentCannotFindSwitchToConnect = 752,
        AuxEquipmentCannotFindCableToConnect = 753
    }

    public static class GeneralErrorToString
    {
        public static string getString(GeneralErrors theErr)
        {
            string errText = "Unknown Error";
            switch (theErr)
            {
                case GeneralErrors.DanglingACLineSegment:
                    return "Dangling cabel";
                case GeneralErrors.DanglingACLineSegmentCloseToBay:
                    return "Dangling cable close to bay";
                case GeneralErrors.DanglingACLineSegmentCloseToEnclosure:
                    return "Dangling cable close to Enclosure";
                case GeneralErrors.UnsupportedACLineSegment:
                    return "Unsupported AC Line Segment";
                case GeneralErrors.DanglingACLineSegmentCloseToConsumer:
                    return "Dangling cabel close to consumer";
                case GeneralErrors.DanglingACLineSegmentCloseToOtherDangling:
                    return "Dangling cabel close to other dangling cabel";
                case GeneralErrors.DanglingACLineSegmentDouble:
                    return "Dangling cabel not connected at both ends";
                case GeneralErrors.DanglingConnectivityEdge:
                    return "Dangling cartographic cabel";
                case GeneralErrors.DanglingConnectivityNotKompunder:
                    return "Dangling cartographic cabel not a component under enclosure or station";
                case GeneralErrors.DanglingConnectivityEdgeOnBusbarSection:
                    return "Cartographic cabel on busbar section, but not connected";
                case GeneralErrors.WrongNumberOfTerminals:
                    return "Wrong Number Of Terminals";
                case GeneralErrors.ComponentHasNoParent:
                    return "No parent";
                case GeneralErrors.ComponentHasParentFarAway:
                    return "Parent too far away";
                case GeneralErrors.ComponentHasNoRootParent:
                    return "Parent has no root parent";
                case GeneralErrors.ComponentHasNoVoltageLevel:
                    return "Component has no voltage level";
                case GeneralErrors.ComponentOverlayAnotherComponent:
                    return "Component Overlay Another Component";
                case GeneralErrors.NameNotUnique:
                    return "Name Not Unique";
                case GeneralErrors.PowerTransformerHasNoConnections:
                    return "Power Transformer Has No Connections";
                case GeneralErrors.PowerTransformerHasNoConnectionToPrimarySide:
                    return "Power Transformer Has No Connection To Primary Side";
                case GeneralErrors.PowerTransformerHasNoConnectionToSecoundarySide:
                    return "Power Transformer Has No Connection To SecoundarySide";
            }
            return errText;
        }
    }

    public enum LineProcessingErrors 
    {
        BayProblemWithLineConnectivity = 600
    }

    public static class LineProcessingErrorToString
    {
        public static string getString(LineProcessingErrors theErr)
        {
            string errText = "Unknown Error";
            switch (theErr)
            {
                case LineProcessingErrors.BayProblemWithLineConnectivity:
                    return "Bay problem with line connectivity";
            }
            return errText;
        }
    }

    public enum TopologyProcessingErrors
    {
        CustomerNoFeed = 2000,
        CustomerMultiFeed = 2001,
        CustomerMultiFeedFromSameNode = 2002,
        TransformerNoFeed = 2010,
        TransformerMultiFeed = 2011,
        TransformerNotFound = 2012
    }

    public static class TopologyProcessingErrorToString
    {
        public static string getString(TopologyProcessingErrors theErr)
        {
            string errText = "Unknown Error";
            switch (theErr)
            {
                case TopologyProcessingErrors.CustomerNoFeed:
                    return "Customer has no feeder";
                case TopologyProcessingErrors.CustomerMultiFeed:
                    return "Customer is multi feeded";
                case TopologyProcessingErrors.CustomerMultiFeedFromSameNode:
                    return "Customer is multi feeded from same node";
                case TopologyProcessingErrors.TransformerNoFeed:
                    return "A transformer in the substation is not feeded";
                case TopologyProcessingErrors.TransformerMultiFeed:
                    return "A transformer in the substation is multi feeded";
                case TopologyProcessingErrors.TransformerNotFound:
                    return "No transformer found in substation";
            }
            return errText;
        }
    }



}
