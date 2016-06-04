CREATE PROCEDURE [dbo].[spGetQuestions]
AS
	SELECT QuestionId,
		QuestionText,
		CategoryId,
		QuestionDifficulty
	FROM Questions
RETURN 0
