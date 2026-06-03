SET NOCOUNT ON;

DECLARE @TenantId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE TenantId = @TenantId)
BEGIN
    DECLARE @SkillGroups TABLE
    (
        GroupOrder INT IDENTITY(1,1) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Skills NVARCHAR(MAX) NOT NULL
    );

    INSERT INTO @SkillGroups (Category, Skills)
    VALUES
    (N'Software Engineer / Backend Engineer', N'Java|Spring Boot|Hibernate|JPA|REST APIs|Microservices|Node.js|Express.js|NestJS|Python|Django|Flask|FastAPI|C#|.NET|.NET Core|ASP.NET|PHP|Laravel|Ruby on Rails|Go|Kotlin|Scala|SQL|SQL Server|PostgreSQL|MySQL|MongoDB|Redis|Kafka|RabbitMQ|Elasticsearch|GraphQL|Docker|Kubernetes|AWS|Azure|GCP|Git|CI/CD|Unit Testing|System Design|API Design|Design Patterns|Clean Architecture'),
    (N'Frontend Engineer', N'JavaScript|TypeScript|React|Next.js|Angular|Vue.js|Nuxt.js|HTML|CSS|SCSS|Tailwind CSS|Bootstrap|Redux|Zustand|React Query|Webpack|Vite|Material UI|Ant Design|Responsive Design|Cross-Browser Compatibility|REST API Integration|GraphQL|Jest|Cypress|Playwright|Figma to HTML|Accessibility|Web Performance Optimization'),
    (N'Full Stack Engineer', N'JavaScript|TypeScript|React|Next.js|Angular|Vue.js|Node.js|Express.js|NestJS|Java|Spring Boot|Python|Django|Flask|.NET Core|REST APIs|GraphQL|PostgreSQL|MySQL|MongoDB|Redis|Docker|Kubernetes|AWS|Azure|Firebase|Supabase|CI/CD|Git|Authentication|Authorization|OAuth|JWT|Microservices|System Design'),
    (N'Mobile App Developer', N'Android|Kotlin|Java|iOS|Swift|SwiftUI|Objective-C|Flutter|Dart|React Native|Expo|Firebase|REST APIs|GraphQL|SQLite|Realm|Push Notifications|App Store Deployment|Play Store Deployment|Mobile UI/UX|Offline Storage|In-App Purchases|Maps Integration|Crashlytics|Mobile Performance Optimization'),
    (N'Data Scientist', N'Python|R|SQL|Pandas|NumPy|Scikit-learn|TensorFlow|PyTorch|Keras|Machine Learning|Deep Learning|NLP|Computer Vision|Statistical Modeling|Predictive Modeling|Feature Engineering|Data Cleaning|Data Visualization|Matplotlib|Seaborn|Plotly|Jupyter Notebook|A/B Testing|Hypothesis Testing|Regression|Classification|Clustering|Time Series Forecasting|Model Evaluation|MLOps|MLflow'),
    (N'Data Engineer', N'Python|SQL|Spark|PySpark|Hadoop|Airflow|Kafka|dbt|ETL|ELT|Data Warehousing|Data Lakes|Snowflake|BigQuery|Redshift|Azure Data Factory|AWS Glue|Databricks|PostgreSQL|MySQL|MongoDB|NoSQL|Data Modeling|Data Pipelines|Batch Processing|Stream Processing|Data Quality|Data Governance'),
    (N'AI / Machine Learning Engineer', N'AI/ML|Python|Machine Learning|Deep Learning|TensorFlow|PyTorch|Scikit-learn|NLP|Computer Vision|Transformers|LLMs|LangChain|LlamaIndex|OpenAI API|Hugging Face|Vector Databases|Pinecone|Weaviate|FAISS|ChromaDB|Embeddings|RAG|Prompt Engineering|Model Fine-tuning|Model Deployment|MLflow|Docker|Kubernetes|FastAPI|MLOps'),
    (N'DevOps Engineer', N'DevOps|Linux|Shell Scripting|Docker|Kubernetes|Helm|Terraform|Ansible|Jenkins|GitHub Actions|GitLab CI/CD|Azure DevOps|AWS|Azure|GCP|Nginx|Apache|Load Balancing|Monitoring|Prometheus|Grafana|ELK Stack|CloudWatch|Infrastructure as Code|CI/CD Pipelines|Networking|Security|SSL|DNS|Autoscaling|Serverless|Bash|Python'),
    (N'QA Engineer / SQA', N'QA Automation|Manual Testing|Automation Testing|Selenium|Cypress|Playwright|Appium|Postman|API Testing|Regression Testing|Smoke Testing|Sanity Testing|Functional Testing|Non-Functional Testing|Performance Testing|JMeter|Load Testing|Security Testing|Test Cases|Test Plans|Bug Reporting|Jira|TestRail|Agile Testing|Mobile Testing|Web Testing|Database Testing|SQL'),
    (N'UI/UX Designer', N'Figma|Adobe XD|Sketch|Wireframing|Prototyping|User Research|User Flows|Information Architecture|Design Systems|UI Design|UX Design|Interaction Design|Responsive Design|Mobile App Design|Web App Design|Usability Testing|Accessibility|Journey Mapping|Persona Creation|Visual Design|Typography|Color Theory|Design Handoff'),
    (N'Product Manager', N'Product Strategy|Roadmap Planning|Requirement Gathering|User Stories|PRD Writing|Agile|Scrum|Kanban|Stakeholder Management|Market Research|Competitor Analysis|Product Discovery|MVP Planning|Backlog Management|Prioritization|Jira|Confluence|Analytics|A/B Testing|User Research|Customer Interviews|Go-to-Market Strategy'),
    (N'Project Manager / Scrum Master', N'Project Planning|Agile|Scrum|Kanban|Sprint Planning|Daily Standups|Risk Management|Resource Management|Timeline Management|Budget Management|Stakeholder Communication|Jira|Trello|Asana|MS Project|Confluence|Team Coordination|Reporting|Dependency Management|Conflict Resolution|Delivery Management|Change Management'),
    (N'Business Analyst', N'Requirement Gathering|Requirement Documentation|BRD|FRD|User Stories|Use Cases|Process Mapping|Gap Analysis|Stakeholder Interviews|Wireframes|Data Analysis|SQL|Excel|Power BI|Jira|Confluence|Agile|Scrum|Acceptance Criteria|UAT|Business Process Modeling|Workflow Analysis|Documentation'),
    (N'HR / Recruiter', N'Talent Acquisition|Technical Recruitment|Non-Technical Recruitment|Candidate Screening|Interview Scheduling|Job Posting|Job Description Writing|LinkedIn Recruiter|Boolean Search|Applicant Tracking Systems|Resume Screening|Sourcing|Onboarding|Employee Engagement|HR Operations|Payroll Coordination|Performance Management|HR Policies|Offer Management|Employee Relations|Background Verification'),
    (N'Sales / Business Development', N'Lead Generation|B2B Sales|B2C Sales|Cold Calling|Cold Emailing|LinkedIn Prospecting|CRM|HubSpot|Salesforce|Zoho CRM|Pipeline Management|Client Communication|Proposal Writing|Negotiation|Closing Deals|Account Management|Market Research|Sales Strategy|Upselling|Cross-Selling|Customer Relationship Management|Presentation Skills|Demo Calls'),
    (N'Marketing', N'Digital Marketing|SEO|SEM|Google Ads|Meta Ads|LinkedIn Ads|Content Marketing|Email Marketing|Social Media Marketing|Marketing Strategy|Campaign Management|Google Analytics|Keyword Research|Copywriting|Branding|Lead Generation|HubSpot|Mailchimp|Marketing Automation|Conversion Optimization|A/B Testing|Influencer Marketing'),
    (N'Finance / Accounts', N'Accounting|Bookkeeping|QuickBooks|Xero|Excel|Financial Reporting|Budgeting|Forecasting|Payroll|Taxation|Auditing|Accounts Payable|Accounts Receivable|Bank Reconciliation|Financial Analysis|ERP|SAP|Oracle Financials|Compliance|Invoicing|Expense Management'),
    (N'Customer Support / Customer Success', N'Customer Support|Customer Success|Ticket Management|Zendesk|Freshdesk|Intercom|Live Chat|Email Support|Phone Support|CRM|Client Onboarding|Product Training|Issue Resolution|Escalation Management|Customer Retention|Customer Satisfaction|SLA Management|Communication Skills|Troubleshooting'),
    (N'Cybersecurity Engineer', N'Network Security|Application Security|Cloud Security|Penetration Testing|Vulnerability Assessment|SIEM|SOC|IDS|IPS|Firewalls|OWASP|Ethical Hacking|Burp Suite|Kali Linux|Nmap|Wireshark|IAM|Zero Trust|Security Audits|Incident Response|Threat Modeling|Compliance|ISO 27001|SOC 2|GDPR'),
    (N'Cloud Engineer', N'AWS|Azure|GCP|EC2|S3|Lambda|RDS|CloudFront|IAM|VPC|Azure Functions|Azure App Service|AKS|GKE|Cloud Run|Terraform|CloudFormation|Kubernetes|Docker|Serverless|Monitoring|Cost Optimization|Cloud Security|Networking|Load Balancing|Autoscaling|Disaster Recovery');

    ;WITH Expanded AS
    (
        SELECT
            SkillName = TRIM(split.value),
            NormalizedName = LOWER(TRIM(split.value)),
            skillGroup.Category,
            skillGroup.GroupOrder
        FROM @SkillGroups AS skillGroup
        CROSS APPLY STRING_SPLIT(skillGroup.Skills, N'|') AS split
        WHERE TRIM(split.value) <> N''
    ),
    Deduped AS
    (
        SELECT
            SkillName,
            NormalizedName,
            Category,
            ROW_NUMBER() OVER (PARTITION BY NormalizedName ORDER BY GroupOrder, Category) AS RowNumber
        FROM Expanded
    )
    MERGE dbo.Skills AS target
    USING
    (
        SELECT NEWID() AS SkillId, @TenantId AS TenantId, SkillName AS Name, NormalizedName, Category, N'[]' AS AliasesJson
        FROM Deduped
        WHERE RowNumber = 1
    ) AS source
    ON target.TenantId = source.TenantId AND target.NormalizedName = source.NormalizedName
    WHEN MATCHED THEN
        UPDATE SET Status = N'Active', UpdatedAtUtc = @Now
    WHEN NOT MATCHED THEN
        INSERT (SkillId, TenantId, Name, NormalizedName, Category, AliasesJson, IsVectorRelevant, Status, CreatedAtUtc, UpdatedAtUtc)
        VALUES (source.SkillId, source.TenantId, source.Name, source.NormalizedName, source.Category, source.AliasesJson, 1, N'Active', @Now, @Now);
END;
