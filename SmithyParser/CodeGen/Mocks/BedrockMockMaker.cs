using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Bedrock;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Newtonsoft.Json;

namespace SmithyParser.CodeGen.Mocks
{

    

    public class OpenAIMockMaker
    {
        public async Task MakeMock()
        {



        }
    }
    public class BedrockMockMaker
    {
        IAmazonBedrock bedrock = new AmazonBedrockClient();
        private IAmazonBedrockRuntime bedrockRuntime = new AmazonBedrockRuntimeClient();
        public async Task MakeMock(string implementationPrompt, string interfaceToMock, string sourceCodeFile)
        {
            var sourceCode = File.OpenText(sourceCodeFile).ReadToEnd();




            var body = new
            {
                prompt = $"\n\nHuman: Build an implementation of the {interfaceToMock} interface in C# as a class. {implementationPrompt}. Here is the code:\n\n{sourceCode}\n\nAssistant:",
                max_tokens_to_sample = 2048,
                temperature = 0.5,
                top_k = 250,
                top_p = 1,
                anthropic_version = "bedrock-2023-05-31"
            };
            var json = JsonConvert.SerializeObject(body);
            var memoryStream = ConvertStringToMemoryStream(json);


            var resp = await bedrockRuntime.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = "anthropic.claude-v2:1",
                ContentType = "application/json",
                Accept = "*/*",
                Body = memoryStream
            });

            var text = ConvertToString(resp.Body);



        }

       
            public MemoryStream ConvertStringToMemoryStream(string inputString)
            {
                if (inputString == null)
                    throw new ArgumentNullException(nameof(inputString));

                // Convert the string to a byte array using a specified encoding (e.g., UTF-8)
                byte[] byteArray = Encoding.UTF8.GetBytes(inputString);

                // Create a new MemoryStream from the byte array
                MemoryStream memoryStream = new MemoryStream(byteArray);

                // Optionally, you can set the position of the MemoryStream to the beginning
                memoryStream.Seek(0, SeekOrigin.Begin);

                return memoryStream;
            }
        

 
            public string ConvertToString(MemoryStream memoryStream)
            {
                if (memoryStream == null)
                    throw new ArgumentNullException(nameof(memoryStream));

                memoryStream.Position = 0; // Reset the position of MemoryStream to the beginning.

                using (StreamReader reader = new StreamReader(memoryStream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        

    }
}
