using System.Net;
using AwesomeAssertions;
using KustoLoco.Geo;

namespace GeoTests;

/// <summary>
///     geo_info_from_ip_address(ip).country is the name ADX gives the country, not .NET's region name.
///     Each expected name is what the ADX engine (kustainer) returns for addresses DB-IP places in that
///     country; every other country already had ADX's name.
/// </summary>
[TestClass]
public class CountryNameTests
{
    [TestMethod]
    [DataRow("AG", "Antigua and Barbuda")]
    [DataRow("BA", "Bosnia and Herzegovina")]
    [DataRow("CG", "Congo Republic")]
    [DataRow("CI", "Ivory Coast")]
    [DataRow("FM", "Federated States of Micronesia")]
    [DataRow("HK", "Hong Kong")]
    [DataRow("KN", "Saint Kitts and Nevis")]
    [DataRow("KR", "South Korea")]
    [DataRow("LC", "Saint Lucia")]
    [DataRow("MO", "Macao")]
    [DataRow("NL", "The Netherlands")]
    [DataRow("PM", "Saint Pierre and Miquelon")]
    [DataRow("PS", "Palestine")]
    [DataRow("SH", "St. Helena")]
    [DataRow("SJ", "Svalbard and Jan Mayen")]
    [DataRow("ST", "São Tomé and Príncipe")]
    [DataRow("TC", "Turks and Caicos Islands")]
    [DataRow("TT", "Trinidad and Tobago")]
    [DataRow("UM", "United States Minor Outlying Islands")]
    [DataRow("VC", "Saint Vincent and the Grenadines")]
    [DataRow("WF", "Wallis and Futuna")]
    [DataRow("US", "United States")]
    [DataRow("GB", "United Kingdom")]
    [DataRow("DE", "Germany")]
    public void CountryIsTheNameAdxGives(string code, string name)
    {
        var provider = DbIpGeoProvider.FromLines([$"10.0.0.0,10.0.0.255,{code},1.5,2.5"], DbIpLayout.CountryCentroid);
        provider.Lookup(IPAddress.Parse("10.0.0.7"))!.Country.Should().Be(name);
    }
}
