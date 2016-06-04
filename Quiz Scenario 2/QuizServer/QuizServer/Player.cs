using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuizServer
{
    public sealed class Player
    {
        public string PlayerName { get; set; }

        public string Gender { get; set; }

        public int Age { get; set; }

        public string Location { get; set; }

        public Guid PlayerId { get; set; }

        public IList<Question> QuestionsAsked { get; set; }

        public Player()
        {
            QuestionsAsked = new List<Question>();
        }

        public string GetPlayerBlobName()
        {
            return (PlayerName.Replace(" ", ".") + "-" + Location.Replace(" ", "_")).ToLower();
        }
    }
}
