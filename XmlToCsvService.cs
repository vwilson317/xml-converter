using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using Xml2CSharp;

public static class XmlToCsvService
{
    public static void ConvertXmlsToCsvs(string inputDirectoryPath, string schemaFilePath)
    {
        // Load schema file
        XmlSchemaSet schemaSet = new XmlSchemaSet();
        schemaSet.Add("", schemaFilePath);

        // Create XML reader settings with schema validation
        XmlReaderSettings readerSettings = new XmlReaderSettings();
        readerSettings.Schemas = schemaSet;
        readerSettings.ValidationType = ValidationType.Schema;
        readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        readerSettings.ValidationEventHandler += ValidationCallback;

        // Configure CSV writer settings
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ",",
        };

        // Get list of XML files in input directory
        string[] xmlFilePaths = Directory.GetFiles(inputDirectoryPath, "*.xml");

        // Convert each XML file to a CSV file
        foreach (string xmlFilePath in xmlFilePaths)
        {
            string csvFilePath = Path.ChangeExtension(xmlFilePath, "csv");

            // Read XML elements and write CSV rows
            using (XmlReader reader = XmlReader.Create(xmlFilePath, readerSettings))
            using (var writer = new StreamWriter(csvFilePath))
            using (var csv = new CsvWriter(writer, csvConfig))
            {
                csv.WriteHeader<CsvRow>();

                var serializer = new XmlSerializer(typeof(ZCUST));
                var data = serializer.Deserialize(reader) as ZCUST;

                var props = typeof(CsvRow).GetProperties();
                var row = new CsvRow();

                foreach (var prop in props)
                {
                    var attribute = prop.GetAttribute<PathAttribute>();
                    var value = GetValueByPath(data, attribute.Value, attribute.Separator);
                    // var path = prop.GetCustomAttributes(typeof(Path), false)[0] as Path;
                    // var value = GetValueByPath<string>(data, path.Value);
                    prop.SetValue(row, value);
                }
                // Write CSV rows
                //csv.WriteRecords(rows.Row);
            }
        }
    }

    private static void ValidationCallback(object sender, ValidationEventArgs e)
    {
        if (e.Severity == XmlSeverityType.Error)
        {
            throw new XmlSchemaValidationException(e.Message);
        }
    }

    private static object GetValueByPath(object obj, string propPath, char? seperator)
    {
        var propNames = propPath.Split('.');
        foreach (var propName in propNames)
        {
            if (propName.Contains("&"))
            {
                var comboProps = propName.Split('&');
                var valueList = new List<dynamic>();
                foreach (var comboProp in comboProps)
                {
                    if (comboProp.Contains("="))
                    {
                        var propNameAndFilter = comboProp.Split('=');
                        var filtered = ((IEnumerable<dynamic>)obj).Select(x =>
                            x.GetType().GetProperties().Where(x => x.Name == propNameAndFilter[0]
                            && x.GetValue(obj, null) == propNameAndFilter[1]));
                        // obj.GetType().GetProperties().Where(x => x.Name == propNameAndFilter[0]).ToList().ForEach(x =>
                        // {
                        //     var prop = x.GetValue(obj, null);
                        //     valueList.Add(prop);
                        // }
                        //var collection = GetCollection(obj, comboProp);
                    }
                    else
                    {
                        var prop = obj.GetType().GetProperty(comboProp).GetValue(obj, null);
                        valueList.Add(prop);
                    }
                }
                if (seperator.HasValue)
                {
                    return string.Join(seperator.Value, valueList);
                }
                else
                {
                    return string.Concat(valueList);
                }
            }
            if (propName.Contains("="))
            {
                obj = GetCollection(obj, propName);
            }
            else
            {
                var prop = obj.GetType().GetProperty(propName);
                obj = prop.GetValue(obj, null);
            }
        }

        return obj;
    }

    private static object GetCollection(object obj, string propName)
    {
        var value = propName.Split('=')[1];
        if (int.TryParse(value, out int index))
        {
            return ((IEnumerable<dynamic>)obj).ElementAt(index - 1);
        }
        else
        {
            //assume that value is a string
            return ((IEnumerable<dynamic>)obj).Where(x => x == value);
        }
    }
}