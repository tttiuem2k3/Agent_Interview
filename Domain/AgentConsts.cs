namespace AI_Agent_Basic.Domain
{
    public static class AgentConsts
    {
        public const double PassThreshold = 60.0;
        public const int MaxQuestions = 6;
        public const string ExitCommand = "exit";

        // MySQL XAMPP
        public const string MySqlConnection = "server=localhost;port=4123;database=auto_interview;user=root;password=;CharSet=utf8mb4;";
        public static readonly System.Version MySqlServerVersion = new System.Version(10, 4, 32);
    }
}
