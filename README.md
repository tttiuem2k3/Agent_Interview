# 📌 Agent AI Interviewer in C#
This project is an automated AI agent designed to conduct initial screening interviews for Asoft company. Built with C#, the agent leverages Large Language Models (LLMs) and a structured prompt system to create a natural, friendly, and professional interview experience.
Developed by intern Trần Tấn Thịnh (Employee ID: AS.0484).

---

### 🚀 Key Features
The AI agent manages the entire initial interview process through a series of automated steps:

### 👋 Friendly Greeting
The agent starts by introducing itself as Asoft's automated interview assistant to create a comfortable and professional atmosphere.

### 📋 Candidate Information Collection
The agent asks for the candidate's full name, email, and phone number, then analyzes and stores this information in a database.

### 🎯 Role and Level Selection
The agent presents a list of open positions and asks the candidate to specify the role and experience level (Fresher, Junior, Senior) they are applying for.

### 📄 Dynamic Job Description
Based on the candidate's selection, the agent retrieves the job description and required skills from the database to present a clear and encouraging summary.

### 🎤 Interactive Interview Session
The agent conducts the interview by asking a series of questions related to the job role and level. After each answer, the agent provides instant and constructive feedback.

### 💯 Automated Scoring
Each answer is scored based on keywords and semantic similarity to a sample answer. The score is then saved in the answers table in the database.

### ⚖️ Automated Pass/Fail Handling

✅ Pass: If the candidate's total score is 60 or higher, they pass the screening round. The agent announces the result and automatically schedules a second-round interview with the technical team lead.

❌ Fail: If the score is below 60, the agent provides a subtle and constructive feedback.

📧 Email Notifications: For successful candidates, the system automatically generates and sends demo emails to both the candidate and the relevant team lead to confirm the details of the next interview round.

---

## ⚙️ How It Works
Initiation: The agent begins the conversation with a friendly greeting.

Data Collection: The agent collects the candidate's contact information and the position they are interested in. This data is then saved to the candidates table.

Context Setup: The agent retrieves job details from the positions table to explain the role to the candidate.

Q&A: Using questions from the interview_questions table, the agent asks questions to the candidate one by one.

Evaluation: The candidate's answers are evaluated, and the results (score and feedback) are stored in the answers table.

Conclusion: The agent calculates the final total score. If the candidate passes, a new appointment is created in the schedules table, and notification emails are prepared.

---

## 🛠️ Technologies Used
Backend Language: C#.
### 🧠 AI: The agent integrates with Large Language Models via clients such as GeminilimClient.cs and OpenAiLlmClient.cs.

### 💾 Database: The system uses a SQL database, managed via Entity Framework Core through AgentDbContext.cs.

### ✨ Prompt Engineering: A core component of the project, with various techniques used to guide the LLM's behavior.

---

## 📂 Project Structure
The project is organized into logical layers for easy maintenance and clarity:

### 📁 Domain
Contains core data models (entities) such as Candidate, Position, InterviewSession, and Answer.

### 📁 Infrastructure
Manages data access, primarily containing the database context (AgentDbContext.cs).

### 📁 Services
Holds the business logic for functions like scoring (ScoringService.cs), scheduling (SchedulingService.cs), and email sending (EmailService.cs).

### 📁 Tools
Includes external utilities, like QuestionBankTool.cs to retrieve interview questions.

### 📁 Agent
The central orchestrating component (AgentOrchestrator.cs) that manages the entire interview flow.

---

## 💬 Key Prompts Used
The agent's intelligence and conversational flow are guided by a set of carefully designed prompts.

### 📜 System Prompt
This general prompt defines the AI's role, rules, and overall workflow from start to finish.

"You are an automated AI Interview Agent for Asoft company. Communicate naturally, warmly, and professionally; personalize the language based on the context. The process: 1) Short introduction... 2) Ask the user... 3) ...present and ask which position the user wants to apply for... 4) ...friendly exchange about the position description... 5) Interview... 6) Conclusion: calculate the total score... 7) Always maintain a professional attitude..."

### 🔧 Key Tool and User Prompts
To extract candidate information:
"Extract contact information from the candidate's response. Return a valid JSON with the following fields: { 'name': string, 'email': string, 'phone': string }"

To score answers:
"You are a technical judge. Score the semantic accuracy of the candidate's answer compared to the sample answer. Return a JSON with the schema: { 'semantic_score': number, 'reasoning': string }. 0 = completely wrong, 1 = very close to the sample."

To generate final result messages:
"Generate a polite, positive concluding remark. If Pass (>=60 points): announce that they passed the interview round and will be scheduled for round 2. If Fail (<60 points): provide subtle feedback, suggesting areas for improvement."

---

## Sample Email Prompts:
To Candidate:
"[ASOFT] INVITATION FOR ROUND 2 INTERVIEW. Hello {candidate.FullName}, You have passed the automated interview round for the position of {position.Name}..."

To Leader:
"[ASOFT] ROUND 2 INTERVIEW SCHEDULE NOTIFICATION. Hello {leader.FullName}, Candidate {candidate.FullName} has passed the automated interview round..."
