namespace DevOpsPlatform.Core.Enums;

public enum NodeType
{
    Technology,
    Topic,
    Subtopic,
    Skill,
    Lesson,
    Lab,
    Question,
    Exam,
    Certification,
    Project
}

public enum EdgeType
{
    Prereq,
    Covers,
    MapsTo,
    Replaces,
    DependsOn,
    PartOf
}

public enum LabType
{
    Guided,
    Practice,
    Troubleshooting,
    Challenge,
    Project,
    Certification
}

public enum LabStatus
{
    Pending,
    Provisioning,
    Running,
    Completed,
    Failed,
    Expired,
    Cancelled
}

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    TrueFalse,
    Ordering,
    Matching,
    Scenario,
    Troubleshooting,
    Code,
    Config,
    Practical
}

public enum Difficulty
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}
