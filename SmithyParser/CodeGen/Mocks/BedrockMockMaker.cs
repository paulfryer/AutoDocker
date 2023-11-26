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
        public async Task MakeMock()
        {

            var body = new
            {
                prompt = "\n\nHuman: Hello world\n\nAssistant:",
                max_tokens_to_sample = 300,
                temperature = 0.5,
                top_k = 250,
                top_p = 1,
                stop_sequences = new[]
                {
                    "\n\nHuman:"
                },
                anthropic_version = "bedrock-2023-05-31"
            };
            var json = JsonConvert.SerializeObject(body);
            var memoryStream = StringToMemoryStreamConverter.ConvertStringToMemoryStream(json);


            var resp = await bedrockRuntime.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = "anthropic.claude-v2",
                ContentType = "application/json",
                Accept = "*/*",
                Body = memoryStream
            });



        }

        public class StringToMemoryStreamConverter
        {
            public static MemoryStream ConvertStringToMemoryStream(string inputString)
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
        }

    }
}
