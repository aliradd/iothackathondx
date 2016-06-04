CREATE PROCEDURE [dbo].[spGetCategorys]
AS
	SELECT CategoryId,
		CategoryName
	FROM Categorys
RETURN 0
