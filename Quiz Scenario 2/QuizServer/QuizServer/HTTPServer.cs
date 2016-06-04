using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using Windows.Devices.Gpio;

namespace QuizServer
{
    public sealed class HTTPServer
    {
        private const uint BUFFERSIZE = 8192;
        private const int MAXQUESTIONS = 7;
        private const string IOTHUBCONNECTIONSTRINGANSWERS = "HostName=<IoTHubName>.azure-devices.net;DeviceId=quiz-server;SharedAccessKey=<SharedAccesKey>";
        private const string IOTHUBCONNECTIONSTRINGPLAYERPROFILE = "HostName=<IoTHubName-Profiles>.azure-devices.net;DeviceId=quiz-server;SharedAccessKey=<SharedAccessKey>";
        private const string QUESTIONSAPIURL = "http://<APIAppName>.azurewebsites.net/api/questions";
        private const int REDLEDPIN = 27;
        private const int GREENLEDPIN = 22;

        private List<Player> players = new List<Player>();

        private List<Question> questions = new List<Question>();

        private StreamSocketListener listener;

        private GpioController gpioController;

        private GpioPin redLEDPin;
        private GpioPin greenLEDPin;
        private GpioPinValue redLEDPinValue = GpioPinValue.High;
        private GpioPinValue greenLEDPinValue = GpioPinValue.High;

        /* TODO: 
            Blob storage for user profile
            IoT Hub
            SQL Database
        */
        public async void Start()
        {
            InitialiseGPIO();

            await InitialiseQuestions();

            listener = new StreamSocketListener();

            await listener.BindServiceNameAsync("80");

            listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            try
            {
                StringBuilder request = new StringBuilder();
                using (IInputStream input = socket.InputStream)
                {
                    byte[] data = new byte[BUFFERSIZE];
                    IBuffer buffer = data.AsBuffer();
                    uint dataRead = BUFFERSIZE;
                    while (dataRead == BUFFERSIZE)
                    {
                        await input.ReadAsync(buffer, BUFFERSIZE, InputStreamOptions.Partial);
                        request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                        dataRead = buffer.Length;
                    }
                }

                using (IOutputStream output = socket.OutputStream)
                {
                    string[] requestMethod = request.ToString().Split('\n');
                    string[] requestParts = requestMethod[0].Split(' ');

                    string body = requestMethod[requestMethod.GetUpperBound(0)].Replace("\0", "");

                    if (requestParts[0] == "GET")
                    {
                        await WriteResponseAsync(requestParts[1], output, Guid.Empty, 0, 0);
                    }
                    else if (requestParts[0] == "POST")
                    {
                        // Extract data from body
                        if (body.ToLower().Contains("playerid"))
                        {
                            Guid playerId = Guid.Empty;

                            string[] postData = body.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                            // Extract player id
                            playerId = GetGuidValue(postData, "playerid");

                            // Validate player id 
                            if ((from p in players where p.PlayerId == playerId select p).Count() == 0)
                            {
                                // Invalid player id. Reset player id
                                playerId = Guid.Empty;
                            }

                            int questionId = GetIntValue(postData, "questionid");

                            int answerId = GetIntValue(postData, "answer");

                            await WriteResponseAsync(requestParts[1], output, playerId, questionId, answerId);
                        }
                        if (body.ToLower().Contains("playername"))
                        {
                            Player player = new Player();

                            string[] postData = body.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                            // Extract player name
                            player.PlayerName = GetStringValue(postData, "playername");

                            // Extract player age
                            player.Age = GetIntValue(postData, "playerage");

                            // Extract gener
                            player.Gender = GetStringValue(postData, "gender");

                            // Extract location
                            player.Location = GetStringValue(postData, "location");

                            // Create new player id
                            player.PlayerId = Guid.NewGuid();

                            players.Add(player);

                            SavePlayerToIoTHub(player).Wait();

                            await WriteResponseAsync(requestParts[1], output, player.PlayerId, 0, 0);
                        }
                        else
                        {
                            // Return to home page
                            await WriteResponseAsync(requestParts[1], output, Guid.Empty, 0, 0);
                        }
                    }
                    else
                    {
                        //throw new InvalidDataException("HTTP method not supported: " + requestParts[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace + "\r\n");
            }
        }

        private async Task WriteResponseAsync(string request, IOutputStream os, Guid playerId, int previousQuestionId, int answerId)
        {
            try
            {
                using (IOutputStream output = os)
                {
                    using (Stream response = output.AsStreamForWrite())
                    {
                        Answer correctAnswer = null;
                        Question previousQuestion = null;
                        Player player = null;

                        if (playerId != Guid.Empty)
                        {
                            player = (from p in players where p.PlayerId == playerId select p).FirstOrDefault();
                        }

                        if (previousQuestionId > 0)
                        {
                            previousQuestion = (from q in player.QuestionsAsked where q.QuestionId == previousQuestionId select q).FirstOrDefault();
                            previousQuestion.TimeAnswered = DateTime.Now;
                            correctAnswer = (from a in previousQuestion.Answers where a.CorrectAnswer == true select a).FirstOrDefault();
                            Answer playerAnswer = (from a in previousQuestion.Answers where a.AnswerId == answerId select a).FirstOrDefault();
                            playerAnswer.PlayerAnswer = true;
                            await SendDeviceToCloudAnswer(player, previousQuestion, playerAnswer.AnswerId);
                        }

                        byte[] bodyArray;
                        if (request == "/favicon.ico")
                        {
                            // Ignore favicon requests
                            bodyArray = Encoding.UTF8.GetBytes("");
                        }
                        else if (playerId == Guid.Empty)
                        {
                            // If player id is an empty guid then assume new game
                            string text = System.IO.File.ReadAllText("PageTemplates\\Default.html");

                            bodyArray = Encoding.UTF8.GetBytes(text);
                        }
                        else if (previousQuestionId < MAXQUESTIONS)
                        {
                            // Get next question - copy so player object has own copy
                            string text = System.IO.File.ReadAllText("PageTemplates\\Question.html");
                            Question originalQuestion = GetNextQuestion(playerId);
                            Question question = new Question() { QuestionDifficulty = originalQuestion.QuestionDifficulty, QuestionId = originalQuestion.QuestionId, QuestionText = originalQuestion.QuestionText, Category = new Category() { CategoryId = originalQuestion.Category.CategoryId, CategoryName = originalQuestion.Category.CategoryName } };
                            question.Answers.Add(new Answer() { AnswerId = originalQuestion.Answers[0].AnswerId, AnswerText = originalQuestion.Answers[0].AnswerText, CorrectAnswer = originalQuestion.Answers[0].CorrectAnswer });
                            question.Answers.Add(new Answer() { AnswerId = originalQuestion.Answers[1].AnswerId, AnswerText = originalQuestion.Answers[1].AnswerText, CorrectAnswer = originalQuestion.Answers[1].CorrectAnswer });
                            question.Answers.Add(new Answer() { AnswerId = originalQuestion.Answers[2].AnswerId, AnswerText = originalQuestion.Answers[2].AnswerText, CorrectAnswer = originalQuestion.Answers[2].CorrectAnswer });
                            question.Answers.Add(new Answer() { AnswerId = originalQuestion.Answers[3].AnswerId, AnswerText = originalQuestion.Answers[3].AnswerText, CorrectAnswer = originalQuestion.Answers[3].CorrectAnswer });

                            if (question.QuestionId == 0)
                            {
                                // No more questions found. End the game
                                bodyArray = Encoding.UTF8.GetBytes("<html><body>No more questions</body></html>");
                            }
                            else
                            {
                                text = text.Replace("{QUESTIONNUMBER}", (player.QuestionsAsked.Count() + 1).ToString());
                                text = text.Replace("{QUESTIONID}", question.QuestionId.ToString());
                                text = text.Replace("{CATEGORYID}", question.Category.CategoryId.ToString());
                                text = text.Replace("{QUESTION}", question.QuestionText);
                                text = text.Replace("{ANSWER1}", question.Answers[0].AnswerText);
                                text = text.Replace("{ANSWER1ID}", question.Answers[0].AnswerId.ToString());
                                text = text.Replace("{ANSWER2}", question.Answers[1].AnswerText);
                                text = text.Replace("{CATEGORY}", question.Category.CategoryName);
                                text = text.Replace("{ANSWER2ID}", question.Answers[1].AnswerId.ToString());
                                text = text.Replace("{ANSWER3}", question.Answers[2].AnswerText);
                                text = text.Replace("{ANSWER3ID}", question.Answers[2].AnswerId.ToString());
                                text = text.Replace("{ANSWER4}", question.Answers[3].AnswerText);
                                text = text.Replace("{ANSWER4ID}", question.Answers[3].AnswerId.ToString());
                                text = text.Replace("{PLAYERID}", playerId.ToString());
                                if (previousQuestionId > 0)
                                {
                                    if (correctAnswer.AnswerId == answerId)
                                    {
                                        text = text.Replace("{PREVIOUSANSWER}", "<span style='color: green'>CORRECT.</span>");
                                        redLEDPinValue = GpioPinValue.High;
                                        greenLEDPinValue = GpioPinValue.Low;
                                        redLEDPin.Write(redLEDPinValue);
                                        greenLEDPin.Write(greenLEDPinValue);
                                    }
                                    else
                                    {
                                        text = text.Replace("{PREVIOUSANSWER}", "<span style='color:red'>WRONG.</span> The correct answer was " + correctAnswer.AnswerText);
                                        redLEDPinValue = GpioPinValue.Low;
                                        greenLEDPinValue = GpioPinValue.High;
                                        redLEDPin.Write(redLEDPinValue);
                                        greenLEDPin.Write(greenLEDPinValue);
                                    }
                                }
                                else
                                {
                                    text = text.Replace("{PREVIOUSANSWER}", "");
                                }
                                if (player.QuestionsAsked.Count + 1 == MAXQUESTIONS)
                                {
                                    text = text.Replace("{SUBMITNAME}", "Complete Quiz");
                                }
                                else
                                {
                                    text = text.Replace("{SUBMITNAME}", "Next Question");
                                }

                                bodyArray = Encoding.UTF8.GetBytes(text);

                                question.TimeAsked = DateTime.Now;
                                player.QuestionsAsked.Add(question);
                            }
                        }
                        else
                        {
                            int correctAnswers = (from p in player.QuestionsAsked where p.Answers.Any(a => a.CorrectAnswer == true && a.PlayerAnswer == true) select p).Count();

                            bodyArray = Encoding.UTF8.GetBytes("<html><body>Congratulations you have completed the quiz. You scored " + correctAnswers.ToString() + " of " + MAXQUESTIONS.ToString() + "<br/><br/><a href=\"/\">Click to start again</a></body></html>");
                        }

                        var bodyStream = new MemoryStream(bodyArray);

                        var header = "HTTP/1.1 200 OK\r\n" +
                                    $"Content-Length: {bodyStream.Length}\r\n" +
                                        "Connection: close\r\n\r\n";

                        byte[] headerArray = Encoding.UTF8.GetBytes(header);
                        await response.WriteAsync(headerArray, 0, headerArray.Length);
                        await bodyStream.CopyToAsync(response);
                        await response.FlushAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private string GetStringValue(string[] keysandvalues, string key)
        {
            string value = string.Empty;

            string tempValue = (from p in keysandvalues where p.ToLower().Contains(key.ToLower()) select p).FirstOrDefault();

            // Replace + with space
            tempValue = tempValue.Replace('+', ' ');

            // Check key has a value
            if (tempValue.Length > tempValue.IndexOf('='))
            {
                value = WebUtility.HtmlDecode(tempValue.Substring(tempValue.IndexOf('=') + 1, tempValue.Length - (tempValue.IndexOf('=') + 1)).Trim());
            }

            return value;
        }

        private int GetIntValue(string[] keysandvalues, string key)
        {
            int value = 0;

            string tempValue = (from p in keysandvalues where p.ToLower().Contains(key.ToLower()) select p).FirstOrDefault();

            // Check key has a value
            if (tempValue.Length > tempValue.IndexOf('='))
            {
                int.TryParse(WebUtility.HtmlDecode(tempValue.Substring(tempValue.IndexOf('=') + 1, tempValue.Length - (tempValue.IndexOf('=') + 1)).Trim()), out value);
            }

            return value;
        }

        private Guid GetGuidValue(string[] keysandvalues, string key)
        {
            Guid value = Guid.Empty;

            string tempValue = (from p in keysandvalues where p.ToLower().Contains(key.ToLower()) select p).FirstOrDefault();

            // Check key has a value
            if (tempValue.Length > tempValue.IndexOf('='))
            {
                Guid.TryParse(WebUtility.HtmlDecode(tempValue.Substring(tempValue.IndexOf('=') + 1, tempValue.Length - (tempValue.IndexOf('=') + 1)).Trim()), out value);
            }

            return value;
        }

        private async Task InitialiseQuestions()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(QUESTIONSAPIURL);
                WebResponse response = await request.GetResponseAsync();
                using (Stream responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                    questions = JsonConvert.DeserializeObject<List<Question>>(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private Question GetNextQuestion(Guid playerId)
        {
            Question question = new Question();

            try
            {
                Player player = (from p in players where p.PlayerId == playerId select p).FirstOrDefault();

                List<Question> eligableQuestion = questions.Where(q => player.QuestionsAsked.All(q2 => q2.QuestionId != q.QuestionId)).ToList();

                if (eligableQuestion.Count > 0)
                {
                    Random r = new Random();
                    question = eligableQuestion.ElementAt(r.Next(0, eligableQuestion.Count() - 1));
                }
                else
                {
                    // Reset question if questionfound is false so the same question isn't returned again
                    question = new Question();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return question;
        }

        //private async Task SavePlayerToStorage(Player player)
        //{
        //    try
        //    {

        //        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(STORAGECONNECTIONSTRING);

        //        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

        //        CloudBlobContainer container = blobClient.GetContainerReference(STORAGECONTAINER);

        //        //await container.CreateIfNotExistsAsync();

        //        CloudBlockBlob blockBlob = container.GetBlockBlobReference(player.GetPlayerBlobName());

        //        await blockBlob.UploadTextAsync("{\"id\":\"" + player.PlayerId.ToString() + "\", \"gender\":\"" + player.Gender + "\", \"age\":" + player.Age.ToString() + ", \"location\":\"" + player.Location + "\"}");
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine(ex.Message);
        //    }
        //}

        private async Task SavePlayerToIoTHub(Player player)
        {
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(IOTHUBCONNECTIONSTRINGPLAYERPROFILE);

            string message = "{\"guid\":\"" + player.PlayerId.ToString() + "\", \"gender\":\"" + player.Gender + "\", \"age\":" + player.Age.ToString() + ", \"location\":\"" + player.Location + "\", \"timecreated\":\"" + DateTime.Now.ToString("o") + "\"}";

            Message eventMessage = new Message(Encoding.UTF8.GetBytes(message));

            await deviceClient.SendEventAsync(eventMessage);
        }

        private async Task SendDeviceToCloudAnswer(Player player, Question question, int answerId)
        {
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(IOTHUBCONNECTIONSTRINGANSWERS);

                string message = "{\"guid\":\"" + player.PlayerId.ToString() + "\", \"category\":\"" + question.Category.CategoryName + "\",\"questionNum\":" +
                    question.QuestionId.ToString() + ",\"answerNum\":" + answerId.ToString() + ",\"timeasked\":\"" + question.TimeAsked.ToString("o") + "\",\"timeanswered\":\"" + question.TimeAnswered.ToString("o") + "\"}";

                Message eventMessage = new Message(Encoding.UTF8.GetBytes(message));

                await deviceClient.SendEventAsync(eventMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void InitialiseGPIO()
        {
            gpioController = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpioController == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create GPIO controller");
                return;
            }
            redLEDPin = gpioController.OpenPin(REDLEDPIN);
            greenLEDPin = gpioController.OpenPin(GREENLEDPIN);
            greenLEDPin.SetDriveMode(GpioPinDriveMode.Output);
            redLEDPin.SetDriveMode(GpioPinDriveMode.Output);
        }
    }
}
