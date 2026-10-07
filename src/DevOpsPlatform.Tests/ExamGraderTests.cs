namespace DevOpsPlatform.Tests;

using DevOpsPlatform.Core.Entities;
using DevOpsPlatform.Core.Enums;
using DevOpsPlatform.Infrastructure.Services;
using System.Text.Json;
using Xunit;

public class ExamGraderTests
{
    private static Question SingleChoice(string correct, decimal? maxScore = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Technology = "linux",
        Level = "basic",
        QuestionType = QuestionType.SingleChoice,
        Prompt = "?",
        CorrectAnswer = JsonDocument.Parse(JsonSerializer.Serialize(correct)),
        Metadata = maxScore.HasValue
            ? JsonDocument.Parse($"{{\"maxScore\": {maxScore}}}")
            : JsonDocument.Parse("{}")
    };

    private static JsonElement El(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void SingleChoice_CorrectAnswer_FullScore()
    {
        var (ok, score, _) = ExamGrader.Grade(SingleChoice("ls -a"), El("\"ls -a\""));
        Assert.True(ok);
        Assert.Equal(10, score);
    }

    [Fact]
    public void SingleChoice_CaseInsensitive_Trimmed()
    {
        var (ok, score, _) = ExamGrader.Grade(SingleChoice("pwd"), El("\"  PWD \""));
        Assert.True(ok);
        Assert.Equal(10, score);
    }

    [Fact]
    public void SingleChoice_Wrong_Zero()
    {
        var (ok, score, _) = ExamGrader.Grade(SingleChoice("pwd"), El("\"cd\""));
        Assert.False(ok);
        Assert.Equal(0, score);
    }

    [Fact]
    public void MaxScore_FromMetadata()
    {
        Assert.Equal(25, ExamGrader.MaxScore(SingleChoice("a", 25)));
        Assert.Equal(10, ExamGrader.MaxScore(SingleChoice("a")));
    }

    [Fact]
    public void MultipleChoice_FullSet_FullScore()
    {
        var q = SingleChoice("x") with
        {
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = JsonDocument.Parse("[\"a\", \"b\"]")
        };
        var (ok, score, _) = ExamGrader.Grade(q, El("[\"b\", \"a\"]"));
        Assert.True(ok);
        Assert.Equal(10, score);
    }

    [Fact]
    public void MultipleChoice_Partial_Credit()
    {
        var q = SingleChoice("x") with
        {
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = JsonDocument.Parse("[\"a\", \"b\"]")
        };
        var (ok, score, _) = ExamGrader.Grade(q, El("[\"a\"]"));
        Assert.False(ok);
        Assert.Equal(5, score);
    }
}
