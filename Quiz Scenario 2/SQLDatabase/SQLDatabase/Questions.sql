CREATE TABLE [dbo].[Questions]
(
	[QuestionId] INT NOT NULL PRIMARY KEY IDENTITY, 
    [QuestionText] VARCHAR(255) NOT NULL, 
    [CategoryId] INT NOT NULL, 
    [QuestionDifficulty] INT NOT NULL
)
