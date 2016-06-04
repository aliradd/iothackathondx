using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using BusinessObjects;

namespace APIApp.Controllers
{
    public class QuestionsController : ApiController
    {
        public IEnumerable<Question> Get()
        {
            List<Question> questions = new List<Question>();
            List<Answer> answers = new List<Answer>();
            List<Category> cateogorys = new List<Category>();

            using (SqlConnection connection = new SqlConnection(ConfigurationManager.ConnectionStrings["sqldatabase"].ConnectionString))
            {
                // Get Questions
                string storedProcedure = "spGetQuestions";

                SqlCommand command = new SqlCommand(storedProcedure, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Question question = new Question();
                        question.QuestionId = (int)reader["QuestionId"];
                        question.QuestionText = reader["QuestionText"].ToString();
                        question.QuestionDifficulty = (int)reader["QuestionDifficulty"];
                        question.Category.CategoryId = (int)reader["CategoryId"];
                        questions.Add(question);
                    }
                    reader.Close();
                }

                // Get Question Answers 
                storedProcedure = "spGetAnswers";
                command = new SqlCommand(storedProcedure, connection);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Answer answer = new Answer();
                        answer.AnswerId = (int)reader["AnswerId"];
                        answer.AnswerText = reader["AnswerText"].ToString();
                        answer.QuestionId = (int)reader["QuestionID"];
                        answer.CorrectAnswer = (bool)reader["CorrectAnswer"];
                        answers.Add(answer);
                    }
                    reader.Close();
                }

                // Get Categorys
                storedProcedure = "spGetCategorys";
                command = new SqlCommand(storedProcedure, connection);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Category category = new Category();
                        category.CategoryId = (int)reader["CategoryId"];
                        category.CategoryName = reader["CategoryName"].ToString();
                        cateogorys.Add(category);
                    }
                    reader.Close();
                }
            }

            // Populate question object with answers and category
            foreach (Question question in questions)
            {
                question.Category = (from c in cateogorys where c.CategoryId == question.Category.CategoryId select c).FirstOrDefault();
                List<Answer> questionAnswers = (from a in answers where a.QuestionId == question.QuestionId select a).ToList();
                foreach (Answer answer in questionAnswers)
                {
                    question.Answers.Add(answer);
                }
            }

            return (IEnumerable<Question>)questions;
               
        }
    }
}
