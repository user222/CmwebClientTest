using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script;
using System.Xml;
using System.IO;

namespace CmwebClientTest
{
    class Program
    {

        static string cmwebServer = "some_server";

        static void Main(string[] args)
        {
        
            if(args.Length < 1) {
                Console.Error.WriteLine("Missing message ID command line argument.");
                Environment.Exit(1);
            }
            if (args.Length > 1)
            {
                Console.Error.WriteLine("Only one command line argument is allowed: message ID");
                Environment.Exit(1);
            }

            //IIRC the message id is a unsigned 32-bit integer.  You could probably just treat it as a string though.
            uint messageId = Convert.ToUInt32(args[0]);

            Console.WriteLine("------ Start " + DateTime.Now);
            try
            {
                int currentMessageInstanceId = basicMessageInfo(messageId);
                componentInfo(messageId);
                buildJobInfo(messageId, currentMessageInstanceId);
            }
            catch(System.Net.WebException e)
            {
                Console.Error.WriteLine("Web request failed with message: \n" + e.ToString());

                if (e.Response != null)
                {
                    //the body of the response should hopefully contain some error details   
                    StreamReader streamReader = new StreamReader(e.Response.GetResponseStream());
                    Console.Error.WriteLine("\nResponse: \n" + streamReader.ReadToEnd());
                }
                
                Environment.Exit(1);
            }
            Console.WriteLine("------ End " + DateTime.Now);
            
        }

        static string getLciBaseUrl()
        {
            return string.Format("http://{0}/lci", cmwebServer);
        }


        static int basicMessageInfo(uint messageId)
        {
            WebClient webClient = new WebClient();
            webClient.Proxy = null;
            string url = string.Format("{0}/message/view/id/{1}/format/xml", getLciBaseUrl(), messageId);  // available formats: xml, json
            string xmlString = webClient.DownloadString(url);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            XmlNode rootNode = doc.DocumentElement;

            Console.WriteLine("Message ID: " + getStringValueFromXmlNode(rootNode, "id"));

            //The message string, severity, and tech notes can be changed by developers.
            // LCI tracks the different versions, or instances, of the message.  The most recent instance is known as the "current instance".
            Console.WriteLine("Message String: " + getStringValueFromXmlNode(rootNode, "currentInstance/string"));
            Console.WriteLine("Tech Notes: " + getStringValueFromXmlNode(rootNode, "currentInstance/techNotes"));
            Console.WriteLine("Severity: " + getStringValueFromXmlNode(rootNode, "currentInstance/severity"));

            Console.WriteLine("State: " + getStringValueFromXmlNode(rootNode, "state"));
            Console.WriteLine("Content Needed: " + getStringValueFromXmlNode(rootNode, "needsContent"));

            Console.WriteLine("Content Description: " + getStringValueFromXmlNode(rootNode, "content/description"));
            Console.WriteLine("Content Resolution: " + getStringValueFromXmlNode(rootNode, "content/resolution"));
            Console.WriteLine("Content Internal Notes: " + getStringValueFromXmlNode(rootNode, "content/internalNotes"));

            //Return the current message instance id for later use.
            return Convert.ToInt32(getStringValueFromXmlNode(rootNode, "currentMessageInstanceId"));
        }


        static void componentInfo(uint messageId)
        {
            WebClient webClient = new WebClient();
            webClient.Proxy = null;
            string url = string.Format("{0}/message/get-components/id/{1}/format/xml", getLciBaseUrl(), messageId);
            string xmlString = webClient.DownloadString(url);
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlString);
            XmlNode rootNode = doc.DocumentElement;

            string components = "";

            foreach (XmlNode item in rootNode.SelectNodes("item"))
            {
                components = components + item.FirstChild.Value + ", ";
            }

            Console.WriteLine("Components: " + components);
        }


        static void buildJobInfo(uint messageId, int currentMessageInstanceId)
        {
            WebClient webClient = new WebClient();
            webClient.Proxy = null;

            //The currentMessageInstanceId was obtained from basicMessageInfo().
            // We use limit == 1 to get max and min.  Note: limit == 0 will return all list results (if the request doesn't time out).
            string lastBuildJobUrl = string.Format("{0}/build-job-import/list-build-job-message-instance/messageInstanceId/{1}/limit/1/format/xml/sortField/buildJobId/sortDirection/DESC", getLciBaseUrl(), currentMessageInstanceId);
            string firstBuildJobUrl = string.Format("{0}/build-job-import/list-build-job-message-instance/messageId/{1}/limit/1/format/xml/sortField/buildJobId/sortDirection/ASC", getLciBaseUrl(), messageId);

            string lastBuildJobXmlString = webClient.DownloadString(lastBuildJobUrl);
            XmlDocument lastBuildJobDoc = new XmlDocument();
            lastBuildJobDoc.LoadXml(lastBuildJobXmlString);
            XmlNode lastBuildJobRootNode = lastBuildJobDoc.DocumentElement;

            //The subcomponent listed on the view message web page comes from the last build job
            Console.WriteLine("Subcomponent: " + getStringValueFromXmlNode(lastBuildJobRootNode, "item/componentName"));
            Console.WriteLine(string.Format("Last Build: {0} ({1})",
                getStringValueFromXmlNode(lastBuildJobRootNode, "item/buildJob/mainBuildArtifact/label"),
                getStringValueFromXmlNode(lastBuildJobRootNode, "item/buildJob/created")));

            string firstBuildJobXmlString = webClient.DownloadString(firstBuildJobUrl);
            XmlDocument firstBuildJobDoc = new XmlDocument();
            firstBuildJobDoc.LoadXml(firstBuildJobXmlString);
            XmlNode firstBuildJobRootNode = firstBuildJobDoc.DocumentElement;

            Console.WriteLine(string.Format("First Build: {0} ({1})",
                getStringValueFromXmlNode(firstBuildJobRootNode, "item/buildJob/mainBuildArtifact/label"),
                getStringValueFromXmlNode(firstBuildJobRootNode, "item/buildJob/created")));

        }


        static string getStringValueFromXmlNode(XmlNode xmlNode, string xPathString)
        {
            XmlNode childNode = xmlNode.SelectSingleNode(xPathString);
            return (childNode != null && childNode.HasChildNodes) ? childNode.FirstChild.Value : "";
        }

    }
}
