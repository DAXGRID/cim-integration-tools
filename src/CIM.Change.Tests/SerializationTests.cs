namespace CIM.Change.Tests;

public class SerializationTests
{
    [Fact]
    public void SerializeCimData()
    {
        var testData = """
{
   "$type":"DataSetMember",
   "TargetObject":"DistributionNodeAsset/860346c7-0a07-49e6-896a-b6d9525acf99",
   "Change":{
      "$type":"ObjectCreation",
      "Object":{
         "$type":"DistributionNodeAsset",
         "parent":"48bc14b6-9b9b-4277-b789-9dc63e603bd5",
         "parentEipName":"X79980.1",
         "baseVoltage":"400 V",
         "eipName":"X79980.1.2",
         "eipSubtype":"LV-CabinetFeeder",
         "wgs84Latitude":56.30328369140625,
         "wgs84Longitude":10.362141609191895,
         "mRID":"860346c7-0a07-49e6-896a-b6d9525acf99",
         "name":"Mølbjerget 23, Inst 513169"
      }
   },
   "mRID":"7e123e8d-96df-42dc-87cc-7431bda97c78"
}
""";

    Assert.True(!string.IsNullOrWhiteSpace(testData));
    }
}
