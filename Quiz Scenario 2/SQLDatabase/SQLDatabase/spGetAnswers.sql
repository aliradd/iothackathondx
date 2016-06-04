CREATE PROCEDURE [dbo].[spGetAnswers]
AS
	SELECT AnswerId,
		AnswerText,
		CorrectAnswer,
		QuestionID
	FROM Answers
RETURN 0
