# 🎤 AI Interview Agent in C#

> An automated **AI screening interviewer for ASOFT** built with C#/.NET, LLM integration, Entity Framework Core and MySQL.

---

## 📌 Introduction

Agent_Interview automates the first screening round of a recruitment process. The agent collects candidate information, understands the applied position and level, asks interview questions, evaluates answers, calculates a score and produces the final screening result.

The source is structured around a clear separation between orchestration, domain models, database access, LLM clients and business tools.

---

## 🚀 Key Features

- 👋 Friendly AI-driven interview greeting.
- 👤 Candidate information collection.
- 🎯 Position and level selection.
- 📄 Dynamic job-description lookup from the database.
- 💬 Interactive interview Q&A.
- 🧠 LLM-assisted normalization and understanding of candidate input.
- 💯 Automated scoring and feedback.
- ✅ Automatic Pass / Fail decision.
- 📅 Scheduling flow for the next interview round.
- 📧 Demo email notifications for successful candidates.
- 🔄 Support for both Gemini and OpenAI-compatible LLM clients.

---

## 🧠 Interview Flow

~~~text
Start Interview
      │
      ▼
Collect Name / Email / Phone
      │
      ▼
Select Position + Level
      │
      ▼
Load Job Description
      │
      ▼
Load Interview Questions
      │
      ▼
Ask Question
      │
      ▼
Candidate Answer
      │
      ├──► LLM Feedback
      └──► Scoring Service
              │
              ▼
         Total Score
              │
       ┌──────┴──────┐
       ▼             ▼
     PASS           FAIL
       │
       ▼
Schedule Round 2
+ Email Notification
~~~

The current source uses a configurable pass threshold through AgentConsts.

---

## 🏗️ Architecture

| Layer | Responsibility |
|---|---|
| Agent | Main interview orchestration |
| Domain | Constants and domain models |
| Infrastructure | Entity Framework Core / MySQL database |
| Services | LLM clients, scoring and supporting services |
| Tools | Question retrieval, matching, scheduling and email tools |
| Program.cs | Composition root / application entry point |

---

## 🛠️ Technologies Used

- 💻 **C#**
- 🟣 **.NET 9**
- 🗄️ **Entity Framework Core 9**
- 🐬 **MySQL / MariaDB**
- 🔗 **Pomelo.EntityFrameworkCore.MySql**
- ✨ **Google Gemini API**
- 🤖 **OpenAI Chat Completions API**
- 🧠 LLM-based information normalization and feedback

---

## 📂 Project Structure

~~~text
Agent_Interview/
├── Agent/
│   └── AgentOrchestrator.cs
├── Domain/
│   └── AgentConsts.cs
├── Infrastructure/
│   └── AgentDbContext.cs
├── Services/
│   ├── ILlmClient.cs
│   ├── GeminiLlmClient.cs
│   └── OpenAiLlmClient.cs
├── Tools/
├── Program.cs
├── AI_Agent_Basic.csproj
└── AI_Agent_Basic.sln
~~~

---

## ⚙️ Installation

### 1. Clone repository

~~~bash
git clone https://github.com/tttiuem2k3/Agent_Interview.git
cd Agent_Interview
~~~

### 2. Requirements

- .NET SDK 9
- MySQL/MariaDB
- A configured LLM API key
- Database schema required by the interview application

### 3. Restore packages

~~~bash
dotnet restore
~~~

### 4. Database

The current source uses MySQL through Entity Framework Core. Update the database connection in the local configuration/source for your environment before running.

Do not commit real database passwords or production credentials.

### 5. Run

~~~bash
dotnet run
~~~

The application starts as a console-based AI interview session.

---

## 🤖 LLM Providers

The project defines the common ILlmClient contract and currently includes:

- GeminiLlmClient
- OpenAiLlmClient

This keeps AgentOrchestrator independent from a single LLM provider.

---

## 📈 Screening Logic

The agent:

1. Normalizes candidate contact information.
2. Matches position and experience level.
3. Loads interview questions.
4. Gives AI-generated feedback after each answer.
5. Calculates the session score.
6. Returns Pass/Fail based on the configured threshold.
7. Triggers next-round scheduling and demo notifications when applicable.

---

## 🔒 Security Note

API keys and production database credentials should be moved to environment variables or a secret manager before production deployment.

---

## 📞 Contact

- 📧 Email: tttiuem2k3@gmail.com
- 👥 LinkedIn: [Thịnh Trần](https://www.linkedin.com/in/thinh-tran-04122k3/)
- 💬 Zalo / Phone: +84 329966939 | +84 336639775

---
