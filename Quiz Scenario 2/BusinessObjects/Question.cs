using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects
{
    public sealed class Question
    {
        public int QuestionId { get; set; }

        public string QuestionText { get; set; }

        public Category Category { get; set; }

        public IList<Answer> Answers { get; set; }

        public int QuestionDifficulty { get; set; }

        public DateTimeOffset TimeAsked { get; set; }

        public DateTimeOffset TimeAnswered { get; set; }

        public Question()
        {
            Category = new Category();
            Answers = new List<Answer>();
        }
    }
}
