CREATE TABLE [dbo].[Answers]
(
	[AnswerId] INT NOT NULL PRIMARY KEY IDENTITY, 
    [AnswerText] VARCHAR(255) NOT NULL, 
    [CorrectAnswer] BIT NOT NULL DEFAULT 0, 
    [QuestionID] INT NOT NULL
)
