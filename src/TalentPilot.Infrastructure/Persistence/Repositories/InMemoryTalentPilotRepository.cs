using System.Text.Json;
using TalentPilot.Application.Admin.AiSettings;
using TalentPilot.Application.Admin.AuditLogs;
using TalentPilot.Application.Admin.CandidateSources;
using TalentPilot.Application.Admin.Departments;
using TalentPilot.Application.Admin.Groups;
using TalentPilot.Application.Admin.HiringPipelines;
using TalentPilot.Application.Admin.Notifications;
using TalentPilot.Application.Admin.Roles;
using TalentPilot.Application.Admin.Skills;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Application.Admin.Users;
using TalentPilot.Application.Admin.Workflows;
using TalentPilot.Application.Auth;
using TalentPilot.Application.Calendar;
using TalentPilot.Application.Notifications;
using TalentPilot.Common.Time;
using TalentPilot.Domain.Access;
using TalentPilot.Domain.Notifications;
using TalentPilot.Domain.Tenancy;

namespace TalentPilot.Infrastructure.Persistence.Repositories;

public sealed class InMemoryTalentPilotRepository :
    IIdentityRepository,
    IAdminTenantProfileRepository,
    IAdminUsersRepository,
    IAdminAccessPoliciesRepository,
    IAdminDepartmentsRepository,
    IAdminGroupsRepository,
    IAdminRolesRepository,
    IAdminSkillsRepository,
    IAdminNotificationsRepository,
    IAdminAuditLogRepository,
    IAdminAiSettingsRepository,
    IAdminCandidateSourcesRepository,
    IAdminWorkflowsRepository,
    IAdminHiringPipelinesRepository,
    IRealtimeNotificationRepository,
    INotificationEmailProviderSettingsResolver,
    INotificationOutboxProcessor,
    INotificationWorkerStatusStore,
    IGoogleCalendarConnectionRepository
{
    private const int NotificationWorkerPollIntervalSeconds = 30;
    private const int NotificationWorkerStaleAfterSeconds = 90;

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SystemActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkflowDefinitionId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000001");
    private static readonly Guid TransitionCreateByPresalesId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000201");
    private static readonly Guid TransitionForwardRecruiterId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000202");
    private static readonly Guid TransitionInterviewId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000203");
    private static readonly Guid TransitionHiringManagerId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000204");
    private static readonly Guid TransitionRecommendEmployeesId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000205");
    private static readonly Guid TransitionPresalesReturnPmoId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000206");
    private static readonly Guid TransitionPresalesAcceptInternalId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000207");
    private const string DemoPasswordHash = "$2a$10$394j2/GNOR2jpagThC4RWOCkDm2HrM4Mb5nCBrkW3D5OTyQKsH4Nu";
    private static readonly (string Category, string Skills)[] ExpandedSkillCatalog =
    [
        ("Software Engineer / Backend Engineer", "Java|Spring Boot|Hibernate|JPA|REST APIs|Microservices|Node.js|Express.js|NestJS|Python|Django|Flask|FastAPI|C#|.NET|.NET Core|ASP.NET|PHP|Laravel|Ruby on Rails|Go|Kotlin|Scala|SQL|SQL Server|PostgreSQL|MySQL|MongoDB|Redis|Kafka|RabbitMQ|Elasticsearch|GraphQL|Docker|Kubernetes|AWS|Azure|GCP|Git|CI/CD|Unit Testing|System Design|API Design|Design Patterns|Clean Architecture"),
        ("Frontend Engineer", "JavaScript|TypeScript|React|Next.js|Angular|Vue.js|Nuxt.js|HTML|CSS|SCSS|Tailwind CSS|Bootstrap|Redux|Zustand|React Query|Webpack|Vite|Material UI|Ant Design|Responsive Design|Cross-Browser Compatibility|REST API Integration|GraphQL|Jest|Cypress|Playwright|Figma to HTML|Accessibility|Web Performance Optimization"),
        ("Full Stack Engineer", "JavaScript|TypeScript|React|Next.js|Angular|Vue.js|Node.js|Express.js|NestJS|Java|Spring Boot|Python|Django|Flask|.NET Core|REST APIs|GraphQL|PostgreSQL|MySQL|MongoDB|Redis|Docker|Kubernetes|AWS|Azure|Firebase|Supabase|CI/CD|Git|Authentication|Authorization|OAuth|JWT|Microservices|System Design"),
        ("Mobile App Developer", "Android|Kotlin|Java|iOS|Swift|SwiftUI|Objective-C|Flutter|Dart|React Native|Expo|Firebase|REST APIs|GraphQL|SQLite|Realm|Push Notifications|App Store Deployment|Play Store Deployment|Mobile UI/UX|Offline Storage|In-App Purchases|Maps Integration|Crashlytics|Mobile Performance Optimization"),
        ("Data Scientist", "Python|R|SQL|Pandas|NumPy|Scikit-learn|TensorFlow|PyTorch|Keras|Machine Learning|Deep Learning|NLP|Computer Vision|Statistical Modeling|Predictive Modeling|Feature Engineering|Data Cleaning|Data Visualization|Matplotlib|Seaborn|Plotly|Jupyter Notebook|A/B Testing|Hypothesis Testing|Regression|Classification|Clustering|Time Series Forecasting|Model Evaluation|MLOps|MLflow"),
        ("Data Engineer", "Python|SQL|Spark|PySpark|Hadoop|Airflow|Kafka|dbt|ETL|ELT|Data Warehousing|Data Lakes|Snowflake|BigQuery|Redshift|Azure Data Factory|AWS Glue|Databricks|PostgreSQL|MySQL|MongoDB|NoSQL|Data Modeling|Data Pipelines|Batch Processing|Stream Processing|Data Quality|Data Governance"),
        ("AI / Machine Learning Engineer", "AI/ML|Python|Machine Learning|Deep Learning|TensorFlow|PyTorch|Scikit-learn|NLP|Computer Vision|Transformers|LLMs|LangChain|LlamaIndex|OpenAI API|Hugging Face|Vector Databases|Pinecone|Weaviate|FAISS|ChromaDB|Embeddings|RAG|Prompt Engineering|Model Fine-tuning|Model Deployment|MLflow|Docker|Kubernetes|FastAPI|MLOps"),
        ("DevOps Engineer", "DevOps|Linux|Shell Scripting|Docker|Kubernetes|Helm|Terraform|Ansible|Jenkins|GitHub Actions|GitLab CI/CD|Azure DevOps|AWS|Azure|GCP|Nginx|Apache|Load Balancing|Monitoring|Prometheus|Grafana|ELK Stack|CloudWatch|Infrastructure as Code|CI/CD Pipelines|Networking|Security|SSL|DNS|Autoscaling|Serverless|Bash|Python"),
        ("QA Engineer / SQA", "QA Automation|Manual Testing|Automation Testing|Selenium|Cypress|Playwright|Appium|Postman|API Testing|Regression Testing|Smoke Testing|Sanity Testing|Functional Testing|Non-Functional Testing|Performance Testing|JMeter|Load Testing|Security Testing|Test Cases|Test Plans|Bug Reporting|Jira|TestRail|Agile Testing|Mobile Testing|Web Testing|Database Testing|SQL"),
        ("UI/UX Designer", "Figma|Adobe XD|Sketch|Wireframing|Prototyping|User Research|User Flows|Information Architecture|Design Systems|UI Design|UX Design|Interaction Design|Responsive Design|Mobile App Design|Web App Design|Usability Testing|Accessibility|Journey Mapping|Persona Creation|Visual Design|Typography|Color Theory|Design Handoff"),
        ("Product Manager", "Product Strategy|Roadmap Planning|Requirement Gathering|User Stories|PRD Writing|Agile|Scrum|Kanban|Stakeholder Management|Market Research|Competitor Analysis|Product Discovery|MVP Planning|Backlog Management|Prioritization|Jira|Confluence|Analytics|A/B Testing|User Research|Customer Interviews|Go-to-Market Strategy"),
        ("Project Manager / Scrum Master", "Project Planning|Agile|Scrum|Kanban|Sprint Planning|Daily Standups|Risk Management|Resource Management|Timeline Management|Budget Management|Stakeholder Communication|Jira|Trello|Asana|MS Project|Confluence|Team Coordination|Reporting|Dependency Management|Conflict Resolution|Delivery Management|Change Management"),
        ("Business Analyst", "Requirement Gathering|Requirement Documentation|BRD|FRD|User Stories|Use Cases|Process Mapping|Gap Analysis|Stakeholder Interviews|Wireframes|Data Analysis|SQL|Excel|Power BI|Jira|Confluence|Agile|Scrum|Acceptance Criteria|UAT|Business Process Modeling|Workflow Analysis|Documentation"),
        ("HR / Recruiter", "Talent Acquisition|Technical Recruitment|Non-Technical Recruitment|Candidate Screening|Interview Scheduling|Job Posting|Job Description Writing|LinkedIn Recruiter|Boolean Search|Applicant Tracking Systems|Resume Screening|Sourcing|Onboarding|Employee Engagement|HR Operations|Payroll Coordination|Performance Management|HR Policies|Offer Management|Employee Relations|Background Verification"),
        ("Sales / Business Development", "Lead Generation|B2B Sales|B2C Sales|Cold Calling|Cold Emailing|LinkedIn Prospecting|CRM|HubSpot|Salesforce|Zoho CRM|Pipeline Management|Client Communication|Proposal Writing|Negotiation|Closing Deals|Account Management|Market Research|Sales Strategy|Upselling|Cross-Selling|Customer Relationship Management|Presentation Skills|Demo Calls"),
        ("Marketing", "Digital Marketing|SEO|SEM|Google Ads|Meta Ads|LinkedIn Ads|Content Marketing|Email Marketing|Social Media Marketing|Marketing Strategy|Campaign Management|Google Analytics|Keyword Research|Copywriting|Branding|Lead Generation|HubSpot|Mailchimp|Marketing Automation|Conversion Optimization|A/B Testing|Influencer Marketing"),
        ("Finance / Accounts", "Accounting|Bookkeeping|QuickBooks|Xero|Excel|Financial Reporting|Budgeting|Forecasting|Payroll|Taxation|Auditing|Accounts Payable|Accounts Receivable|Bank Reconciliation|Financial Analysis|ERP|SAP|Oracle Financials|Compliance|Invoicing|Expense Management"),
        ("Customer Support / Customer Success", "Customer Support|Customer Success|Ticket Management|Zendesk|Freshdesk|Intercom|Live Chat|Email Support|Phone Support|CRM|Client Onboarding|Product Training|Issue Resolution|Escalation Management|Customer Retention|Customer Satisfaction|SLA Management|Communication Skills|Troubleshooting"),
        ("Cybersecurity Engineer", "Network Security|Application Security|Cloud Security|Penetration Testing|Vulnerability Assessment|SIEM|SOC|IDS|IPS|Firewalls|OWASP|Ethical Hacking|Burp Suite|Kali Linux|Nmap|Wireshark|IAM|Zero Trust|Security Audits|Incident Response|Threat Modeling|Compliance|ISO 27001|SOC 2|GDPR"),
        ("Cloud Engineer", "AWS|Azure|GCP|EC2|S3|Lambda|RDS|CloudFront|IAM|VPC|Azure Functions|Azure App Service|AKS|GKE|Cloud Run|Terraform|CloudFormation|Kubernetes|Docker|Serverless|Monitoring|Cost Optimization|Cloud Security|Networking|Load Balancing|Autoscaling|Disaster Recovery")
    ];

    private readonly object _gate = new();
    private readonly IClock _clock;
    private readonly TenantState _tenant;
    private readonly List<RoleState> _roles = [];
    private readonly List<UserState> _users = [];
    private readonly List<GroupState> _groups = [];
    private readonly List<DepartmentState> _departments = [];
    private readonly List<SkillState> _skills = [];
    private readonly List<CandidateSourceLabelState> _candidateSourceLabels = [];
    private readonly List<InterviewTemplateState> _interviewTemplates = [];
    private readonly List<InterviewTemplateRoundState> _interviewTemplateRounds = [];
    private readonly List<IntakeRoutingRuleState> _intakeRoutingRules = [];
    private readonly List<PermissionCatalogItem> _permissions = [];
    private readonly List<RefreshTokenRecord> _refreshTokens = [];
    private readonly List<GoogleCalendarOAuthState> _googleCalendarOAuthStates = [];
    private readonly List<GoogleCalendarConnection> _googleCalendarConnections = [];
    private readonly List<NotificationEventState> _notificationEvents = [];
    private readonly List<NotificationTemplateState> _notificationTemplates = [];
    private readonly List<NotificationRecipientState> _notificationRecipients = [];
    private readonly List<AiAgentDefinitionState> _aiAgents = [];
    private readonly List<OutboxState> _outbox = [];
    private readonly List<AuditLogState> _auditLogs = [];
    private NotificationWorkerHeartbeat? _notificationWorkerHeartbeat;
    private DateTimeOffset? _notificationWorkerLastHeartbeatUtc;
    private DateTimeOffset? _notificationWorkerLastProcessedAtUtc;

    private readonly TenantAiSettingsState _aiSettings = new()
    {
        TenantId = TenantId,
        Provider = "Mock/Ollama",
        LlmModel = "llama3.2",
        EmbeddingModel = "nomic-embed-text",
        EmbeddingDimensions = 768,
        VectorStore = "SqlServerVector",
        ModelSwitchingLocked = true,
        HumanReviewRequired = true,
        AutoRejectEnabled = false
    };

    private PermissionResolutionMode _permissionResolutionMode = PermissionResolutionMode.MergeAllAssignedRoles;
    private Guid _benchVisibilityRoleId;

    public InMemoryTalentPilotRepository(IClock clock)
    {
        _clock = clock;
        _tenant = new TenantState
        {
            TenantId = TenantId,
            DisplayName = "TKXEL",
            Slug = "tkxel",
            Domain = "tkxel.com",
            AdminContactEmail = "admin@tkxel.com",
            DefaultTimezone = "Asia/Karachi",
            DefaultCurrency = "PKR",
            Status = TenantStatus.Active,
            CareerDisplayName = "TKXEL Careers",
            CompanyAddress = "75-C/II, Gulberg III",
            CompanyCity = "Lahore",
            CompanyCountry = "Pakistan",
            OfficialEmail = "hr@tkxel.com",
            OfficialPhone = "+92 42 111 859 351",
            PrimaryColor = "#0A66C2",
            CandidateLoginRequired = true,
            CandidateCvFormat = "DOCX",
            PublicJobsEnabled = true,
            InviteExpiryDays = 7,
            ReapplyCooldownDays = 90,
            NotificationEmailProvider = NotificationEmailProviders.Resend,
            SetupComplete = true,
            UpdatedAtUtc = _clock.UtcNow
        };

        SeedPermissions();
        SeedRoles();
        SeedGroups();
        SeedSkills();
        SeedUsers();
        SeedDepartments();
        SeedIntakeRoutingRules();
        SeedCandidateSourceLabels();
        SeedInterviewTemplates();
        SeedNotifications();
        SeedAiAgents();
        SeedAuditLogs();
    }

    public Task<AdminAiRuntimeResponse?> GetRuntimeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_aiSettings.TenantId != tenantId)
            {
                return Task.FromResult<AdminAiRuntimeResponse?>(null);
            }

            return Task.FromResult<AdminAiRuntimeResponse?>(new AdminAiRuntimeResponse(
                _aiSettings.Provider,
                _aiSettings.LlmModel,
                _aiSettings.EmbeddingModel,
                _aiSettings.EmbeddingDimensions,
                _aiSettings.VectorStore,
                !_aiSettings.ModelSwitchingLocked));
        }
    }

    public Task<IReadOnlyList<AdminAiAgentDefinition>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AdminAiAgentDefinition>>(
                _aiAgents
                    .Where(agent => agent.Enabled)
                    .OrderBy(agent => agent.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(agent => new AdminAiAgentDefinition(
                        agent.Id,
                        agent.DisplayName,
                        agent.Responsibility,
                        agent.InputSummary,
                        agent.OutputSummary,
                        agent.MvpBoundary,
                        agent.Enabled))
                    .ToArray());
        }
    }

    public Task<AdminAiGuardrailSettings?> GetGuardrailsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_aiSettings.TenantId != tenantId)
            {
                return Task.FromResult<AdminAiGuardrailSettings?>(null);
            }

            return Task.FromResult<AdminAiGuardrailSettings?>(new AdminAiGuardrailSettings(
                _aiSettings.HumanReviewRequired,
                _aiSettings.AutoRejectEnabled));
        }
    }

    public Task<AdminCandidateSourcesResponse> ListAsync(
        Guid tenantId,
        AdminCandidateSourcesQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var materialized = _candidateSourceLabels
                .Where(source => source.TenantId == tenantId)
                .Where(source => MatchesCandidateSourceSearch(source, query.Search))
                .OrderBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(source => new AdminCandidateSourceListItem(
                    source.CandidateSourceLabelId,
                    source.Code,
                    source.DisplayName,
                    source.ReportingCategory,
                    source.Status,
                    source.UpdatedAtUtc))
                .ToArray();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            var summary = new AdminCandidateSourcesSummary(
                materialized.Count(source => source.Status == "Active"),
                materialized.Select(source => source.ReportingCategory).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                materialized.Count(source => source.Status == "Inactive"));

            return Task.FromResult(new AdminCandidateSourcesResponse(summary, items, query.Page, query.PageSize, materialized.Length));
        }
    }

    public Task<AdminWorkflowConfigurationResponse> GetConfigurationAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (tenantId != TenantId)
            {
                return Task.FromResult(new AdminWorkflowConfigurationResponse(
                    new AdminWorkflowSummary(0, 0, 0, 0, 0, 0),
                    [],
                    [],
                    [],
                    []));
            }

            var recruitmentGroup = _groups.First(group => group.Name == "Recruiting - Delivery");
            var updatedAtUtc = _clock.UtcNow;

            var definitions = new[]
            {
                new AdminWorkflowDefinitionItem(
                    WorkflowDefinitionId,
                    "JOB_REQUEST_MVP",
                    "Job Request MVP Workflow",
                    "JobRequest",
                    "Active",
                    updatedAtUtc)
            };
            var stages = new[]
            {
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000101"), "DRAFT", "Draft", 10, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000102"), "PMO_REVIEW", "PMO Review", 20, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000108"), "PRESALES_REVIEW", "Presales Review", 25, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000103"), "SOURCING", "Recruiter Sourcing", 30, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000104"), "INTERVIEWING", "Interviewing", 40, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000105"), "HIRING_MANAGER_REVIEW", "Hiring Manager Review", 50, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000106"), "OFFER", "Offer", 60, false, "Active"),
                new AdminWorkflowStageItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000107"), "CLOSED", "Closed", 70, true, "Active")
            };
            var routingRules = new[]
            {
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000301"), TransitionCreateByPresalesId, "CREATE_BY_PRESALES", "Create by Presales", "Draft", "PMO Review", "DynamicResolver", "DepartmentIntakeRoute", "DepartmentIntakeRoute", "Active"),
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000305"), TransitionRecommendEmployeesId, "RECOMMEND_EMPLOYEES_TO_PRESALES", "Recommend Employees to Presales", "PMO Review", "Presales Review", "DynamicResolver", "SelectedPresalesUser", "SelectedPresalesUser", "Active"),
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000306"), TransitionPresalesReturnPmoId, "PRESALES_RETURN_TO_PMO", "Presales Return to PMO", "Presales Review", "PMO Review", "DynamicResolver", "PMOReferralOwner", "PMOReferralOwner", "Active"),
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000302"), TransitionForwardRecruiterId, "FORWARD_TO_RECRUITER", "Forward to Recruiter", "PMO Review", "Recruiter Sourcing", "Group", recruitmentGroup.Name, string.Empty, "Active"),
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000303"), TransitionInterviewId, "MOVE_TO_INTERVIEWING", "Move to Interviewing", "Recruiter Sourcing", "Interviewing", "DynamicResolver", "CandidateInterviewRounds", "CandidateInterviewRounds", "Active"),
                new AdminWorkflowRoutingRuleItem(Guid.Parse("99999999-aaaa-bbbb-cccc-000000000304"), TransitionHiringManagerId, "FORWARD_TO_HIRING_MANAGER", "Forward to Hiring Manager", "Interviewing", "Hiring Manager Review", "DynamicResolver", "JobRequestHiringManager", "JobRequestHiringManager", "Active")
            };
            var intakeRoutingRules = _departments
                .Where(department => department.TenantId == tenantId && department.Status == "Active")
                .OrderBy(department => department.Name, StringComparer.OrdinalIgnoreCase)
                .Select(department =>
                {
                    var rule = _intakeRoutingRules.FirstOrDefault(item =>
                        item.TenantId == tenantId &&
                        item.DepartmentId == department.DepartmentId);
                    var targetUser = rule?.TargetUserId is null
                        ? null
                        : _users.FirstOrDefault(user => user.UserId == rule.TargetUserId.Value);
                    var targetGroup = rule?.TargetGroupId is null
                        ? null
                        : _groups.FirstOrDefault(group => group.GroupId == rule.TargetGroupId.Value);
                    var assignmentTarget = rule?.AssignmentType switch
                    {
                        "User" => targetUser?.DisplayName ?? "Configured user",
                        "Group" => targetGroup?.Name ?? "Configured group",
                        _ => "Tenant Admin fallback"
                    };
                    var status = rule?.Status ?? "Missing";

                    return new AdminWorkflowIntakeRoutingRuleItem(
                        rule?.JobRequestIntakeRoutingRuleId,
                        department.DepartmentId,
                        department.Code,
                        department.Name,
                        rule?.AssignmentType ?? "Fallback",
                        rule?.TargetUserId,
                        rule?.TargetGroupId,
                        assignmentTarget,
                        status,
                        rule is null || rule.Status != "Active");
                })
                .ToArray();
            return Task.FromResult(new AdminWorkflowConfigurationResponse(
                new AdminWorkflowSummary(
                    definitions.Length,
                    stages.Length,
                    routingRules.Length,
                    routingRules.Length,
                    intakeRoutingRules.Count(rule => rule.Status == "Active"),
                    intakeRoutingRules.Count(rule => rule.UsesTenantAdminFallback)),
                definitions,
                stages,
                routingRules,
                intakeRoutingRules));
        }
    }

    public Task UpdateIntakeRoutingAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateAdminWorkflowIntakeRoutingInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var rule in input.Rules)
            {
                var existing = _intakeRoutingRules.FirstOrDefault(item =>
                    item.TenantId == tenantId &&
                    item.DepartmentId == rule.DepartmentId);
                if (existing is null)
                {
                    _intakeRoutingRules.Add(new IntakeRoutingRuleState
                    {
                        JobRequestIntakeRoutingRuleId = Guid.NewGuid(),
                        TenantId = tenantId,
                        DepartmentId = rule.DepartmentId,
                        AssignmentType = rule.AssignmentType,
                        TargetUserId = rule.TargetUserId,
                        TargetGroupId = rule.TargetGroupId,
                        Status = rule.Status
                    });
                    continue;
                }

                existing.AssignmentType = rule.AssignmentType;
                existing.TargetUserId = rule.TargetUserId;
                existing.TargetGroupId = rule.TargetGroupId;
                existing.Status = rule.Status;
            }

            AddAudit(
                actorUserId,
                "WorkflowIntakeRoutingUpdated",
                "JobRequestIntakeRoutingRules",
                null,
                "Department intake routing",
                "Updated department intake routing rules.",
                "Admin Center",
                metadataJson);

            return Task.CompletedTask;
        }
    }

    public Task<bool> ActiveDepartmentIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> departmentIds,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var validDepartmentIds = _departments
                .Where(department => department.TenantId == tenantId && department.Status == "Active")
                .Select(department => department.DepartmentId)
                .ToHashSet();

            return Task.FromResult(departmentIds.All(validDepartmentIds.Contains));
        }
    }

    public Task<bool> ActiveUserIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var validUserIds = _users
                .Where(user => user.TenantId == tenantId && user.AccountStatus == "Active")
                .Select(user => user.UserId)
                .ToHashSet();

            return Task.FromResult(userIds.All(validUserIds.Contains));
        }
    }

    public Task<bool> ActiveGroupIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var validGroupIds = _groups
                .Where(group => group.TenantId == tenantId && group.Status == "Active")
                .Select(group => group.GroupId)
                .ToHashSet();

            return Task.FromResult(groupIds.All(validGroupIds.Contains));
        }
    }

    public Task<AdminHiringPipelineTemplatesResponse> ListTemplatesAsync(
        Guid tenantId,
        AdminHiringPipelineTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var templates = _interviewTemplates
                .Where(template => template.TenantId == tenantId)
                .Select(ToHiringPipelineTemplateItem)
                .ToArray();
            var materialized = templates
                .Where(template => MatchesHiringPipelineSearch(template, query.Search))
                .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            var activeTemplateIds = _interviewTemplates
                .Where(template => template.TenantId == tenantId && template.Status == "Active")
                .Select(template => template.InterviewTemplateId)
                .ToHashSet();
            var activeRounds = _interviewTemplateRounds
                .Where(round =>
                    round.TenantId == tenantId &&
                    round.Status == "Active" &&
                    activeTemplateIds.Contains(round.InterviewTemplateId))
                .ToArray();
            var summary = new AdminHiringPipelineSummary(
                activeTemplateIds.Count,
                _interviewTemplates.Count(template =>
                    template.TenantId == tenantId &&
                    template.Status == "Active" &&
                    template.DepartmentId.HasValue),
                activeRounds.Length,
                activeRounds.Count(round => !round.OwnerUserId.HasValue));

            return Task.FromResult(new AdminHiringPipelineTemplatesResponse(
                summary,
                items,
                query.Page,
                query.PageSize,
                materialized.Length));
        }
    }

    public Task<AdminHiringPipelineTemplateDetails?> GetHiringPipelineTemplateAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _interviewTemplates.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.InterviewTemplateId == templateId);

            return Task.FromResult(template is null ? null : ToHiringPipelineTemplateDetails(template));
        }
    }

    public Task UpdateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _interviewTemplates.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.InterviewTemplateId == templateId);
            if (template is null)
            {
                return Task.CompletedTask;
            }

            template.Name = input.Name;
            template.DepartmentId = input.DepartmentId;
            template.Description = input.Description ?? string.Empty;
            template.Status = input.Status;
            template.UpdatedAtUtc = _clock.UtcNow;

            var existingRounds = _interviewTemplateRounds
                .Where(round => round.TenantId == tenantId && round.InterviewTemplateId == templateId)
                .ToArray();
            var retainedRoundIds = new HashSet<Guid>();
            foreach (var roundInput in input.Rounds.OrderBy(round => round.RoundOrder))
            {
                var roundId = roundInput.InterviewTemplateRoundId.GetValueOrDefault();
                var round = existingRounds.FirstOrDefault(item => item.InterviewTemplateRoundId == roundId);
                if (round is null)
                {
                    round = new InterviewTemplateRoundState
                    {
                        InterviewTemplateRoundId = Guid.NewGuid(),
                        TenantId = tenantId,
                        InterviewTemplateId = templateId
                    };
                    _interviewTemplateRounds.Add(round);
                }

                retainedRoundIds.Add(round.InterviewTemplateRoundId);
                round.RoundOrder = roundInput.RoundOrder;
                round.Name = roundInput.Name;
                round.OwnerRoleId = roundInput.OwnerRoleId;
                round.OwnerUserId = roundInput.OwnerUserId;
                round.DurationMinutes = roundInput.DurationMinutes;
                round.IsRequired = true;
                round.Status = roundInput.Status;
            }

            var nextInactiveOrder = input.Rounds.Count + 1;
            foreach (var omittedRound in existingRounds.Where(round => !retainedRoundIds.Contains(round.InterviewTemplateRoundId)))
            {
                omittedRound.RoundOrder = nextInactiveOrder++;
                omittedRound.Status = "Inactive";
            }

            AddAudit(actorUserId, "InterviewTemplateUpdated", "InterviewTemplate", templateId, template.Name, "Updated interview template.", "Admin Center", metadataJson);
            return Task.CompletedTask;
        }
    }

    public Task CreateTemplateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid templateId,
        UpdateAdminHiringPipelineTemplateInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _interviewTemplates.Add(new InterviewTemplateState
            {
                InterviewTemplateId = templateId,
                TenantId = tenantId,
                DepartmentId = input.DepartmentId,
                Name = input.Name,
                Description = input.Description ?? string.Empty,
                Status = input.Status,
                UpdatedAtUtc = _clock.UtcNow
            });

            foreach (var roundInput in input.Rounds.OrderBy(round => round.RoundOrder))
            {
                _interviewTemplateRounds.Add(new InterviewTemplateRoundState
                {
                    InterviewTemplateRoundId = Guid.NewGuid(),
                    TenantId = tenantId,
                    InterviewTemplateId = templateId,
                    RoundOrder = roundInput.RoundOrder,
                    Name = roundInput.Name,
                    OwnerRoleId = roundInput.OwnerRoleId,
                    OwnerUserId = roundInput.OwnerUserId,
                    DurationMinutes = roundInput.DurationMinutes,
                    IsRequired = true,
                    Status = roundInput.Status
                });
            }

            AddAudit(actorUserId, "InterviewTemplateCreated", "InterviewTemplate", templateId, input.Name, "Created interview template.", "Admin Center", metadataJson);
            return Task.CompletedTask;
        }
    }

    public Task<bool> TemplateNameExistsAsync(
        Guid tenantId,
        string name,
        Guid exceptTemplateId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_interviewTemplates.Any(template =>
                template.TenantId == tenantId &&
                template.InterviewTemplateId != exceptTemplateId &&
                template.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<bool> DepartmentExistsAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_departments.Any(department =>
                department.TenantId == tenantId &&
                department.DepartmentId == departmentId &&
                department.Status == "Active"));
        }
    }

    public Task<bool> RoleIdsExistAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var distinctIds = roleIds.Where(roleId => roleId != Guid.Empty).Distinct().ToArray();
            var matchingCount = _roles.Count(role =>
                role.TenantId == tenantId &&
                role.Status == "Active" &&
                distinctIds.Contains(role.RoleId));

            return Task.FromResult(matchingCount == distinctIds.Length);
        }
    }

    public Task<IReadOnlyList<LoginOption>> ListLoginOptionsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var options = _users
                .Where(user => user.AccountStatus == "Active")
                .Select(user =>
                {
                    var roles = _roles
                        .Where(role => user.RoleIds.Contains(role.RoleId) && role.Status == "Active")
                        .OrderBy(role => role.Priority)
                        .Select(role => new CurrentUserRole(role.RoleId, role.Code, role.Name, role.Priority))
                        .ToArray();

                    var groups = _groups
                        .Where(group => user.GroupIds.Contains(group.GroupId) && group.Status == "Active")
                        .OrderBy(group => group.Name)
                        .Select(group => new CurrentUserGroup(group.GroupId, group.Name, group.Purpose))
                        .ToArray();

                    return new LoginOption(
                        user.UserId,
                        user.DisplayName,
                        user.Email,
                        roles.FirstOrDefault()?.DisplayName ?? "No assigned role",
                        roles,
                        groups);
                })
                .OrderBy(option => option.Roles.Any(role => role.Code == "Candidate"))
                .ThenBy(option => option.Roles.Min(role => role.Priority))
                .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult<IReadOnlyList<LoginOption>>(options);
        }
    }

    public Task<AuthUserRecord?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user is null ? null : ToAuthUserRecord(user));
        }
    }

    public Task<AuthUserRecord?> FindUserByIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            return Task.FromResult(user is null ? null : ToAuthUserRecord(user));
        }
    }

    public Task<CurrentUserData?> GetCurrentUserDataAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is null)
            {
                return Task.FromResult<CurrentUserData?>(null);
            }

            var data = new CurrentUserData
            {
                UserId = user.UserId,
                TenantId = user.TenantId,
                TenantDisplayName = _tenant.DisplayName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PermissionResolutionMode = _permissionResolutionMode
            };

            foreach (var role in _roles.Where(role => user.RoleIds.Contains(role.RoleId) && role.Status == "Active"))
            {
                var roleData = new RoleWithPermissions
                {
                    RoleId = role.RoleId,
                    Code = role.Code,
                    Name = role.Name,
                    Priority = role.Priority
                };

                foreach (var permission in role.PermissionIds)
                {
                    roleData.PermissionIds.Add(permission);
                }

                data.Roles.Add(roleData);
            }

            data.Groups.AddRange(_groups
                .Where(group => user.GroupIds.Contains(group.GroupId) && group.Status == "Active")
                .Select(group => new CurrentUserGroup(group.GroupId, group.Name, group.Purpose)));

            return Task.FromResult<CurrentUserData?>(data);
        }
    }

    public Task TouchLastActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.LastActiveAtUtc = _clock.UtcNow;
                user.UpdatedAtUtc = _clock.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task StoreRefreshTokenAsync(RefreshTokenRecord record, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _refreshTokens.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<RefreshTokenRecord?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_refreshTokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }
    }

    public Task RevokeRefreshTokenAsync(Guid refreshTokenId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var token = _refreshTokens.FirstOrDefault(item => item.RefreshTokenId == refreshTokenId);
            if (token is not null)
            {
                _refreshTokens.Remove(token);
                _refreshTokens.Add(new RefreshTokenRecord
                {
                    RefreshTokenId = token.RefreshTokenId,
                    TenantId = token.TenantId,
                    UserId = token.UserId,
                    TokenHash = token.TokenHash,
                    ExpiresAtUtc = token.ExpiresAtUtc,
                    RevokedAtUtc = revokedAtUtc
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task StoreOAuthStateAsync(GoogleCalendarOAuthState state, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _googleCalendarOAuthStates.RemoveAll(item => item.StateHash == state.StateHash);
            _googleCalendarOAuthStates.Add(state);
        }

        return Task.CompletedTask;
    }

    public Task<GoogleCalendarOAuthState?> ConsumeOAuthStateAsync(
        string stateHash,
        DateTimeOffset consumedAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var state = _googleCalendarOAuthStates.FirstOrDefault(item =>
                item.StateHash == stateHash &&
                item.ConsumedAtUtc is null &&
                item.ExpiresAtUtc > consumedAtUtc);
            if (state is null)
            {
                return Task.FromResult<GoogleCalendarOAuthState?>(null);
            }

            var consumed = state with { ConsumedAtUtc = consumedAtUtc };
            _googleCalendarOAuthStates.Remove(state);
            _googleCalendarOAuthStates.Add(consumed);
            return Task.FromResult<GoogleCalendarOAuthState?>(consumed);
        }
    }

    public Task<GoogleCalendarConnection?> GetConnectionAsync(
        Guid tenantId,
        string provider,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_googleCalendarConnections.FirstOrDefault(connection =>
                connection.TenantId == tenantId &&
                string.Equals(connection.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
                connection.Status == "Connected"));
        }
    }

    public Task SaveConnectionAsync(
        SaveGoogleCalendarConnectionInput input,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _googleCalendarConnections.FirstOrDefault(connection =>
                connection.TenantId == input.TenantId &&
                string.Equals(connection.Provider, input.Provider, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                _googleCalendarConnections.Remove(existing);
            }

            _googleCalendarConnections.Add(new GoogleCalendarConnection(
                input.TenantId,
                input.OrganizerUserId,
                input.OrganizerEmail,
                input.Provider,
                input.RefreshTokenCiphertext ?? existing?.RefreshTokenCiphertext,
                input.AccessTokenCiphertext,
                input.AccessTokenExpiresAtUtc,
                input.Scope,
                "Connected",
                input.ConnectedAtUtc,
                input.UpdatedAtUtc));
        }

        return Task.CompletedTask;
    }

    public Task UpdateAccessTokenAsync(
        Guid tenantId,
        string provider,
        string? accessTokenCiphertext,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _googleCalendarConnections.FirstOrDefault(connection =>
                connection.TenantId == tenantId &&
                string.Equals(connection.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
                connection.Status == "Connected");
            if (existing is not null)
            {
                _googleCalendarConnections.Remove(existing);
                _googleCalendarConnections.Add(existing with
                {
                    AccessTokenCiphertext = accessTokenCiphertext,
                    AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
                    UpdatedAtUtc = updatedAtUtc
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task<TenantProfileSettings?> GetAsync(
        Guid tenantId,
        string configuredLlmModel,
        string configuredEmbeddingModel,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_tenant.TenantId != tenantId)
            {
                return Task.FromResult<TenantProfileSettings?>(null);
            }

            var settings = new TenantProfileSettings(
                _tenant.TenantId,
                _tenant.DisplayName,
                _tenant.Slug,
                _tenant.Domain,
                _tenant.AdminContactEmail,
                _tenant.DefaultTimezone,
                _tenant.DefaultCurrency,
                _tenant.Status,
                _tenant.CareerDisplayName,
                _tenant.CompanyAddress,
                _tenant.CompanyCity,
                _tenant.CompanyCountry,
                _tenant.OfficialEmail,
                _tenant.OfficialPhone,
                _tenant.PrimaryColor,
                _tenant.CandidateLoginRequired,
                _tenant.CandidateCvFormat,
                _tenant.PublicJobsEnabled,
                _tenant.InviteExpiryDays,
                _tenant.ReapplyCooldownDays,
                _tenant.NotificationEmailProvider,
                _users.Count(user => user.TenantId == tenantId && user.AccountStatus == "Active"),
                _roles.Count(role => role.TenantId == tenantId && role.Status == "Active"),
                _tenant.SetupComplete,
                configuredLlmModel,
                configuredEmbeddingModel,
                _tenant.LogoFileName,
                _tenant.LogoContentType,
                _tenant.LogoContentBase64,
                _tenant.UpdatedAtUtc);

            return Task.FromResult<TenantProfileSettings?>(settings);
        }
    }

    public Task<bool> IsSlugAvailableAsync(Guid tenantId, string slug, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_tenant.TenantId == tenantId ||
                !string.Equals(_tenant.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        UpdateTenantProfileSettingsInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_tenant.TenantId != tenantId)
            {
                return Task.CompletedTask;
            }

            _tenant.DisplayName = input.DisplayName.Trim();
            _tenant.Slug = input.Slug.Trim();
            _tenant.Domain = input.Domain.Trim();
            _tenant.AdminContactEmail = input.AdminContactEmail.Trim();
            _tenant.DefaultTimezone = input.DefaultTimezone.Trim();
            _tenant.DefaultCurrency = input.DefaultCurrency.Trim();
            _tenant.Status = input.Status;
            _tenant.CareerDisplayName = input.CareerDisplayName.Trim();
            _tenant.CompanyAddress = NullIfBlank(input.CompanyAddress);
            _tenant.CompanyCity = NullIfBlank(input.CompanyCity);
            _tenant.CompanyCountry = NullIfBlank(input.CompanyCountry);
            _tenant.OfficialEmail = NullIfBlank(input.OfficialEmail);
            _tenant.OfficialPhone = NullIfBlank(input.OfficialPhone);
            _tenant.PrimaryColor = input.PrimaryColor.Trim();
            _tenant.CandidateLoginRequired = input.CandidateLoginRequired;
            _tenant.CandidateCvFormat = input.CandidateCvFormat.Trim().ToUpperInvariant();
            _tenant.PublicJobsEnabled = input.PublicJobsEnabled;
            _tenant.InviteExpiryDays = input.InviteExpiryDays;
            _tenant.ReapplyCooldownDays = input.ReapplyCooldownDays;
            _tenant.NotificationEmailProvider = NotificationEmailProviders.NormalizeOrDefault(input.NotificationEmailProvider);
            _tenant.LogoFileName = string.IsNullOrWhiteSpace(input.LogoContentBase64) ? null : input.LogoFileName?.Trim();
            _tenant.LogoContentType = string.IsNullOrWhiteSpace(input.LogoContentBase64) ? null : input.LogoContentType?.Trim();
            _tenant.LogoContentBase64 = string.IsNullOrWhiteSpace(input.LogoContentBase64) ? null : input.LogoContentBase64;
            _tenant.UpdatedAtUtc = _clock.UtcNow;

            AddAudit(actorUserId, "TenantProfileUpdated", "Tenant", tenantId, "Tenant profile", "Updated tenant profile settings.", "Admin Center", metadataJson);
        }

        return Task.CompletedTask;
    }

    public Task<NotificationEmailProviderSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var provider = _tenant.TenantId == tenantId
                ? _tenant.NotificationEmailProvider
                : NotificationEmailProviders.Resend;

            return Task.FromResult(new NotificationEmailProviderSettings(NotificationEmailProviders.NormalizeOrDefault(provider)));
        }
    }

    public Task<AdminUsersResponse> ListAsync(Guid tenantId, AdminUsersQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var users = _users.Where(user => user.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                users = users.Where(user =>
                    user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.DepartmentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (user.ExperienceYears?.ToString("0.0").Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (user.JoiningDate?.ToString("yyyy-MM-dd").Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    _roles.Any(role => user.RoleIds.Contains(role.RoleId) && role.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    _groups.Any(group => user.GroupIds.Contains(group.GroupId) && group.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            if (query.RoleId.HasValue)
            {
                users = users.Where(user => user.RoleIds.Contains(query.RoleId.Value));
            }

            if (query.GroupId.HasValue)
            {
                users = users.Where(user => user.GroupIds.Contains(query.GroupId.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.AccountStatus))
            {
                users = users.Where(user => string.Equals(user.AccountStatus, query.AccountStatus, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = users
                .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToUserListItem)
                .ToArray();

            var summary = new AdminUsersSummary(
                _users.Count(user => user.TenantId == tenantId && user.AccountStatus == "Active"),
                _groups.Count(group => group.TenantId == tenantId && group.Status == "Active"),
                new BenchVisibilityPolicySummary(_benchVisibilityRoleId, FindRoleName(_benchVisibilityRoleId), "Roles & Permissions"));

            return Task.FromResult(new AdminUsersResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminUserDetails?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            return Task.FromResult(user is null ? null : ToUserDetails(user));
        }
    }

    public Task<Guid?> FindRoleIdByCodeAsync(Guid tenantId, string roleCode, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles
                .Where(role => role.TenantId == tenantId)
                .FirstOrDefault(role => string.Equals(role.Code, roleCode, StringComparison.OrdinalIgnoreCase))
                ?.RoleId);
        }
    }

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_users.Any(user =>
                user.TenantId == tenantId &&
                user.UserId != exceptUserId &&
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<bool> ActiveRolesExistAsync(Guid tenantId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(roleIds.All(roleId => _roles.Any(role => role.TenantId == tenantId && role.RoleId == roleId && role.Status == "Active")));
        }
    }

    public Task<bool> ActiveGroupsExistAsync(Guid tenantId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(groupIds.All(groupId => _groups.Any(group => group.TenantId == tenantId && group.GroupId == groupId && group.Status == "Active")));
        }
    }

    public Task<int> CountActiveTenantAdminsAsync(Guid tenantId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var tenantAdminRoleId = _roles.First(role => role.Code == AccessConstants.TenantAdminRoleCode).RoleId;
            return Task.FromResult(_users.Count(user =>
                user.TenantId == tenantId &&
                user.UserId != exceptUserId &&
                user.AccountStatus == "Active" &&
                user.RoleIds.Contains(tenantAdminRoleId)));
        }
    }

    public Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var userId = Guid.NewGuid();
            _users.Add(new UserState
            {
                UserId = userId,
                TenantId = tenantId,
                DisplayName = input.DisplayName.Trim(),
                Email = input.Email.Trim(),
                Initials = BuildInitials(input.DisplayName),
                AccountStatus = input.AccountStatus,
                RoleIds = input.RoleIds.Distinct().ToList(),
                GroupIds = input.GroupIds.Distinct().ToList(),
                DepartmentName = "Engineering",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            AddAudit(actorUserId, "UserCreated", "User", userId, input.DisplayName, "Created internal user.", "Admin Center", metadataJson);
            return Task.FromResult(userId);
        }
    }

    public Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid userId, SaveAdminUserInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.DisplayName = input.DisplayName.Trim();
                user.Email = input.Email.Trim();
                user.Initials = BuildInitials(input.DisplayName);
                user.AccountStatus = input.AccountStatus;
                user.RoleIds = input.RoleIds.Distinct().ToList();
                user.GroupIds = input.GroupIds.Distinct().ToList();
                user.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "UserUpdated", "User", userId, user.DisplayName, "Updated internal user.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid userId, UpdateAdminUserStatusInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                user.AccountStatus = input.AccountStatus;
                user.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "UserStatusUpdated", "User", userId, user.DisplayName, $"Changed account status to {input.AccountStatus}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task InsertInviteNotificationAsync(Guid tenantId, Guid actorUserId, Guid userId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var user = FindUser(tenantId, userId);
            if (user is not null)
            {
                _outbox.Add(new OutboxState(Guid.NewGuid(), tenantId, "USER_INVITED", "Pending", _clock.UtcNow, null));
                AddAudit(actorUserId, "UserInviteQueued", "User", userId, user.DisplayName, "Queued user invitation email.", "Admin Center", "{}");
            }
        }

        return Task.CompletedTask;
    }

    public Task<BenchVisibilityPolicy?> GetBenchVisibilityPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<BenchVisibilityPolicy?>(new BenchVisibilityPolicy(
                _benchVisibilityRoleId,
                FindRoleName(_benchVisibilityRoleId),
                _tenant.UpdatedAtUtc,
                SystemActorId));
        }
    }

    public Task<bool> RoleIsActiveAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles.Any(role => role.TenantId == tenantId && role.RoleId == roleId && role.Status == "Active"));
        }
    }

    public Task UpdateBenchVisibilityPolicyAsync(Guid tenantId, Guid actorUserId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _benchVisibilityRoleId = roleId;
            _tenant.UpdatedAtUtc = _clock.UtcNow;
            AddAudit(actorUserId, "BenchVisibilityPolicyUpdated", "AccessPolicy", roleId, "Bench visibility", "Updated bench visibility role.", "Admin Center", "{}");
        }

        return Task.CompletedTask;
    }

    public Task<AdminGroupsResponse> ListAsync(Guid tenantId, AdminGroupsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var groups = _groups.Where(group => group.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Purpose))
            {
                groups = groups.Where(group => group.Purpose.Contains(query.Purpose, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                groups = groups.Where(group =>
                    group.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    group.Purpose.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    group.Status.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = groups.OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(group => new AdminGroupListItem(
                    group.GroupId,
                    group.Name,
                    group.Purpose,
                    group.Status,
                    _users.Count(user => user.GroupIds.Contains(group.GroupId))))
                .ToArray();

            return Task.FromResult(new AdminGroupsResponse(items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminDepartmentsResponse> ListAsync(
        Guid tenantId,
        AdminDepartmentsQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var departments = _departments
                .Where(department => department.TenantId == tenantId)
                .Select(ToAdminDepartmentListItem);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                departments = departments.Where(department =>
                    department.Code.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    department.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    department.LeadName.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    department.Status.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = departments
                .OrderBy(department => department.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            var summary = new AdminDepartmentsSummary(
                materialized.Count(department => department.Status == "Active"),
                materialized.Sum(department => department.EmployeeCount),
                materialized.Sum(department => department.OpenJobRequestCount),
                materialized.Count(department => department.Status == "Inactive"));

            return Task.FromResult(new AdminDepartmentsResponse(summary, items, query.Page, query.PageSize, materialized.Length));
        }
    }

    public Task<AdminDepartmentListItem?> GetDepartmentAsync(
        Guid tenantId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var department = _departments.FirstOrDefault(department =>
                department.TenantId == tenantId &&
                department.DepartmentId == departmentId);

            return Task.FromResult(department is null ? null : ToAdminDepartmentListItem(department));
        }
    }

    public Task<bool> DepartmentCodeOrNameExistsAsync(
        Guid tenantId,
        string code,
        string name,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_departments.Any(department =>
                department.TenantId == tenantId &&
                (department.Code.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                 department.Name.Equals(name, StringComparison.OrdinalIgnoreCase))));
        }
    }

    public Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateDepartmentInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var departmentId = Guid.NewGuid();
            _departments.Add(new DepartmentState
            {
                DepartmentId = departmentId,
                TenantId = tenantId,
                Code = input.Code,
                Name = input.Name,
                Status = input.Status,
                UpdatedAtUtc = _clock.UtcNow
            });

            AddAudit(actorUserId, "DepartmentCreated", "Department", departmentId, input.Name, "Created department.", "Admin Center", metadataJson);
            return Task.FromResult(departmentId);
        }
    }

    public Task<AdminGroupListItem?> GetGroupAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var group = _groups.FirstOrDefault(group => group.TenantId == tenantId && group.GroupId == groupId);
            return Task.FromResult(group is null ? null : ToGroupListItem(group));
        }
    }

    public Task<AdminSkillsResponse> ListAsync(Guid tenantId, AdminSkillsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var materialized = _skills
                .Where(skill => skill.TenantId == tenantId)
                .Where(skill => string.IsNullOrWhiteSpace(query.Category) || skill.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase))
                .Where(skill => MatchesSkillSearch(skill, query.Search))
                .OrderBy(skill => skill.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ToAdminSkillListItem)
                .ToArray();

            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();

            var summary = new AdminSkillsSummary(
                materialized.Count(skill => skill.Status == "Active"),
                materialized.Select(skill => skill.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                materialized.Sum(skill => skill.Aliases.Count));

            return Task.FromResult(new AdminSkillsResponse(summary, items, query.Page, query.PageSize, materialized.Length));
        }
    }

    public Task<AdminSkillListItem?> GetSkillAsync(Guid tenantId, Guid skillId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var skill = _skills.FirstOrDefault(skill => skill.TenantId == tenantId && skill.SkillId == skillId);
            return Task.FromResult(skill is null ? null : ToAdminSkillListItem(skill));
        }
    }

    public Task<bool> SkillNormalizedNameExistsAsync(
        Guid tenantId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_skills.Any(skill =>
                skill.TenantId == tenantId &&
                skill.NormalizedName.Equals(normalizedName.Trim(), StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var skillId = Guid.NewGuid();
            _skills.Add(new SkillState
            {
                SkillId = skillId,
                TenantId = tenantId,
                Name = input.Name,
                NormalizedName = normalizedName,
                Category = input.Category,
                Aliases = input.Aliases.ToList(),
                IsVectorRelevant = true,
                Status = input.Status,
                UpdatedAtUtc = _clock.UtcNow
            });

            AddAudit(actorUserId, "SkillCreated", "Skill", skillId, input.Name, "Created skill.", "Admin Center", metadataJson);
            return Task.FromResult(skillId);
        }
    }

    public Task UpdateAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        UpdateSkillInput input,
        string normalizedName,
        string aliasesJson,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _skills.First(skill => skill.TenantId == tenantId && skill.SkillId == skillId);
            existing.Name = input.Name;
            existing.NormalizedName = normalizedName;
            existing.Category = input.Category;
            existing.Aliases = input.Aliases.ToList();
            existing.Status = input.Status;
            existing.UpdatedAtUtc = _clock.UtcNow;

            AddAudit(actorUserId, "SkillUpdated", "Skill", skillId, input.Name, "Updated skill.", "Admin Center", metadataJson);
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid skillId,
        string skillName,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _skills.RemoveAll(skill => skill.TenantId == tenantId && skill.SkillId == skillId);

            AddAudit(actorUserId, "SkillDeleted", "Skill", skillId, skillName, "Deleted skill.", "Admin Center", metadataJson);
            return Task.CompletedTask;
        }
    }

    public Task<bool> GroupNameExistsAsync(
        Guid tenantId,
        string purpose,
        string name,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_groups.Any(group =>
                group.TenantId == tenantId &&
                group.Purpose.Equals(purpose.Trim(), StringComparison.OrdinalIgnoreCase) &&
                group.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<Guid> CreateAsync(
        Guid tenantId,
        Guid actorUserId,
        CreateGroupInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var groupId = Guid.NewGuid();
            _groups.Add(new GroupState
            {
                GroupId = groupId,
                TenantId = tenantId,
                Name = input.Name,
                Purpose = input.Purpose,
                Status = input.Status
            });

            AddAudit(
                actorUserId,
                "GroupCreated",
                "Group",
                groupId,
                input.Name,
                "Created routing group.",
                "Admin Center",
                metadataJson);

            return Task.FromResult(groupId);
        }
    }

    public Task<AdminGroupMembershipResponse> ListMembershipAsync(
        Guid tenantId,
        Guid groupId,
        AdminGroupMembershipQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var group = _groups.First(group => group.TenantId == tenantId && group.GroupId == groupId);
            var candidates = BuildGroupMembershipCandidates(tenantId, groupId);
            var searchedUsers = ApplyGroupMembershipSearch(candidates, query.Search);
            var users = ApplyGroupMembershipFilter(searchedUsers, query.Membership);

            var ordered = users
                .OrderByDescending(user => user.IsMember)
                .ThenBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var items = ordered
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            var summary = new AdminGroupMembershipSummary(
                candidates.Count(user => user.IsMember),
                candidates.Count(user => !user.IsMember),
                searchedUsers.Count(user => user.IsMember),
                searchedUsers.Count(user => !user.IsMember));

            return Task.FromResult(new AdminGroupMembershipResponse(
                ToGroupListItem(group),
                summary,
                items,
                query.Page,
                query.PageSize,
                ordered.Length));
        }
    }

    public Task<bool> InternalUsersExistAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var distinctUserIds = userIds.Distinct().ToArray();
            return Task.FromResult(distinctUserIds.All(userId =>
                _users.Any(user => user.TenantId == tenantId && user.UserId == userId && IsInternalUser(user))));
        }
    }

    public Task<UpdateGroupMembersResult> UpdateMembershipAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid groupId,
        UpdateGroupMembersInput input,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var group = _groups.First(group => group.TenantId == tenantId && group.GroupId == groupId);
            var (userIdsToAdd, userIdsToRemove) = ResolveGroupMembershipChanges(tenantId, groupId, input);
            var addedCount = 0;
            var removedCount = 0;

            foreach (var userId in userIdsToAdd)
            {
                var user = _users.First(item => item.TenantId == tenantId && item.UserId == userId);
                if (!user.GroupIds.Contains(groupId))
                {
                    user.GroupIds.Add(groupId);
                    user.UpdatedAtUtc = _clock.UtcNow;
                    addedCount++;
                }
            }

            foreach (var userId in userIdsToRemove)
            {
                var user = _users.First(item => item.TenantId == tenantId && item.UserId == userId);
                if (user.GroupIds.Remove(groupId))
                {
                    user.UpdatedAtUtc = _clock.UtcNow;
                    removedCount++;
                }
            }

            var memberCount = _users.Count(user => user.TenantId == tenantId && user.GroupIds.Contains(groupId));
            AddAudit(
                actorUserId,
                "GroupMembershipUpdated",
                "Group",
                groupId,
                group.Name,
                $"Updated {group.Name} membership. Added {addedCount}, removed {removedCount}.",
                "Admin Center",
                metadataJson);

            return Task.FromResult(new UpdateGroupMembersResult(addedCount, removedCount, memberCount));
        }
    }

    public Task<AdminRolesResponse> ListAsync(Guid tenantId, AdminRolesQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var roles = _roles.Where(role => role.TenantId == tenantId);
            if (!query.IncludeInactive)
            {
                roles = roles.Where(role => role.Status == "Active");
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                roles = roles.Where(role => role.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = roles.OrderBy(role => role.Priority).ThenBy(role => role.Name).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToRoleSummary)
                .ToArray();

            var summary = new AdminRolesSummary(
                _roles.Count(role => role.TenantId == tenantId && role.Status == "Active"),
                _roles.Count(role => role.TenantId == tenantId && role.Type == "Tenant"),
                _roles.Count(role => role.TenantId == tenantId && role.Type == "Custom"));

            return Task.FromResult(new AdminRolesResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    Task<RoleDetails?> IAdminRolesRepository.GetAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            return Task.FromResult(role is null ? null : ToRoleDetails(role));
        }
    }

    public Task<IReadOnlyList<PermissionCatalogItem>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PermissionCatalogItem>>(_permissions.ToArray());
        }
    }

    public Task<bool> PermissionIdsExistAsync(IReadOnlyCollection<string> permissionIds, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(permissionIds.All(permissionId => _permissions.Any(permission => permission.PermissionId == permissionId)));
        }
    }

    public Task<bool> RoleNameExistsAsync(Guid tenantId, string name, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_roles.Any(role =>
                role.TenantId == tenantId &&
                role.RoleId != exceptRoleId &&
                string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<Guid> CreateAsync(Guid tenantId, Guid actorUserId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var roleId = Guid.NewGuid();
            _roles.Add(new RoleState
            {
                RoleId = roleId,
                TenantId = tenantId,
                Code = input.Name.Replace(" ", string.Empty, StringComparison.Ordinal),
                Name = input.Name.Trim(),
                Type = "Custom",
                Scope = input.Scope,
                Priority = input.Priority,
                Status = input.Status,
                IsProtected = false,
                IsBulkAssignable = true,
                PermissionIds = input.PermissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            });

            AddAudit(actorUserId, "RoleCreated", "Role", roleId, input.Name, "Created role.", "Admin Center", metadataJson);
            return Task.FromResult(roleId);
        }
    }

    public Task UpdateAsync(Guid tenantId, Guid actorUserId, Guid roleId, SaveRoleInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            if (role is not null)
            {
                role.Name = input.Name.Trim();
                role.Scope = input.Scope;
                role.Priority = input.Priority;
                role.Status = input.Status;
                role.PermissionIds = input.PermissionIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                AddAudit(actorUserId, "RoleUpdated", "Role", roleId, role.Name, "Updated role.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid tenantId, Guid actorUserId, Guid roleId, string status, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var role = _roles.FirstOrDefault(item => item.TenantId == tenantId && item.RoleId == roleId);
            if (role is not null)
            {
                role.Status = status;
                AddAudit(actorUserId, "RoleStatusUpdated", "Role", roleId, role.Name, $"Changed role status to {status}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task<PermissionResolutionPolicy?> GetPermissionResolutionPolicyAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<PermissionResolutionPolicy?>(new PermissionResolutionPolicy(
                _permissionResolutionMode.ToString(),
                _tenant.UpdatedAtUtc,
                SystemActorId));
        }
    }

    public Task UpdatePermissionResolutionPolicyAsync(Guid tenantId, Guid actorUserId, string mode, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _permissionResolutionMode = Enum.Parse<PermissionResolutionMode>(mode, ignoreCase: true);
            _tenant.UpdatedAtUtc = _clock.UtcNow;
            AddAudit(actorUserId, "PermissionResolutionPolicyUpdated", "AccessPolicy", tenantId, "Permission resolution", $"Updated permission resolution to {mode}.", "Admin Center", "{}");
        }

        return Task.CompletedTask;
    }

    public Task<RoleUserAssignmentPreview> PreviewUserAssignmentsAsync(Guid tenantId, Guid roleId, RoleUserAssignmentFilterInput input, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = FilterUsersForRoleAssignment(tenantId, input).ToArray();
            var assignable = matching.Where(user => !user.RoleIds.Contains(roleId)).ToArray();
            var sample = assignable.Take(25).Select(ToRoleUserAssignmentPreviewItem).ToArray();

            return Task.FromResult(new RoleUserAssignmentPreview(
                matching.Length,
                matching.Length - assignable.Length,
                assignable.Length,
                sample));
        }
    }

    public Task<BulkAssignRoleUsersResponse> BulkAssignUsersAsync(Guid tenantId, Guid actorUserId, Guid roleId, BulkAssignRoleUsersInput input, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = FilterUsersForRoleAssignment(tenantId, input.Filters).ToArray();
            var targetUsers = input.SelectionMode == "SelectedUsers"
                ? matching.Where(user => input.SelectedUserIds?.Contains(user.UserId) == true).ToArray()
                : matching;

            var assigned = 0;
            foreach (var user in targetUsers.Where(user => !user.RoleIds.Contains(roleId)))
            {
                user.RoleIds.Add(roleId);
                user.UpdatedAtUtc = _clock.UtcNow;
                assigned++;
            }

            var roleName = FindRoleName(roleId);
            AddAudit(actorUserId, "RoleBulkAssigned", "Role", roleId, roleName, $"Bulk assigned {roleName} to {assigned} users.", "Admin Center", "{}");
            return Task.FromResult(new BulkAssignRoleUsersResponse(Guid.NewGuid(), matching.Length, assigned, targetUsers.Length - assigned));
        }
    }

    public Task<AdminNotificationEventsResponse> ListEventsAsync(Guid tenantId, AdminNotificationEventsQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var events = _notificationEvents.Where(item => item.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                events = events.Where(item =>
                    item.EventCode.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    item.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = events.OrderBy(item => item.EventCode).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToNotificationEventListItem)
                .ToArray();

            var summary = new AdminNotificationEventsSummary(
                _notificationEvents.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _notificationTemplates.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Pending"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Sent"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Failed"));

            return Task.FromResult(new AdminNotificationEventsResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminNotificationEventDetails?> GetEventAsync(Guid tenantId, Guid eventId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _notificationEvents.FirstOrDefault(item => item.TenantId == tenantId && item.EventId == eventId);
            if (item is null)
            {
                return Task.FromResult<AdminNotificationEventDetails?>(null);
            }

            var templates = _notificationTemplates
                .Where(template => template.TenantId == tenantId && template.EventCode == item.EventCode)
                .Select(ToNotificationTemplateSummary)
                .ToArray();

            return Task.FromResult<AdminNotificationEventDetails?>(new AdminNotificationEventDetails(
                item.EventId,
                item.EventCode,
                item.Name,
                item.Recipient,
                item.Status,
                templates));
        }
    }

    public Task<AdminNotificationTemplatesResponse> ListTemplatesAsync(
        Guid tenantId,
        AdminNotificationTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var templates = _notificationTemplates.Where(template => template.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                templates = templates.Where(template =>
                    template.EventCode.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    template.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    template.Subject.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    template.Recipient.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = templates
                .OrderBy(template => template.Name)
                .Select(ToNotificationTemplateSummary)
                .ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();
            var summary = new AdminNotificationEventsSummary(
                _notificationEvents.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _notificationTemplates.Count(item => item.TenantId == tenantId && item.Status == "Active"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Pending"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Sent"),
                _outbox.Count(item => item.TenantId == tenantId && item.Status == "Failed"));

            return Task.FromResult(new AdminNotificationTemplatesResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    public Task<AdminNotificationOutboxResponse> ListOutboxAsync(
        Guid tenantId,
        AdminNotificationOutboxQuery query,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var rows = _outbox.Where(item => item.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                rows = rows.Where(item => string.Equals(item.Status, query.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                rows = rows.Where(item =>
                    item.EventCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.Status.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = rows
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToArray();

            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(ToNotificationOutboxItem)
                .ToArray();

            var workerStatus = BuildNotificationWorkerStatus(tenantId);
            return Task.FromResult(new AdminNotificationOutboxResponse(workerStatus, items, query.Page, query.PageSize, materialized.Length));
        }
    }

    public Task<AdminNotificationOutboxItem?> GetOutboxItemAsync(
        Guid tenantId,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var row = _outbox.FirstOrDefault(item => item.TenantId == tenantId && item.OutboxId == outboxId);
            return Task.FromResult(row is null ? null : ToNotificationOutboxItem(row));
        }
    }

    public Task<AdminNotificationOutboxItem?> RequeueOutboxEmailAsync(
        Guid tenantId,
        Guid outboxId,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var row = _outbox.FirstOrDefault(item =>
                item.TenantId == tenantId &&
                item.OutboxId == outboxId &&
                string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                return Task.FromResult<AdminNotificationOutboxItem?>(null);
            }

            row.Status = "Pending";
            row.ProcessedAtUtc = null;
            row.LastError = null;
            return Task.FromResult<AdminNotificationOutboxItem?>(ToNotificationOutboxItem(row));
        }
    }

    public Task<NotificationTemplateSummary?> GetTemplateAsync(Guid tenantId, Guid templateId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _notificationTemplates.FirstOrDefault(item => item.TenantId == tenantId && item.TemplateId == templateId);
            return Task.FromResult(template is null ? null : ToNotificationTemplateSummary(template));
        }
    }

    public Task UpdateTemplateAsync(Guid tenantId, Guid actorUserId, Guid templateId, UpdateNotificationTemplateInput input, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var template = _notificationTemplates.FirstOrDefault(item => item.TenantId == tenantId && item.TemplateId == templateId);
            if (template is not null)
            {
                template.Subject = input.Subject;
                template.Body = input.Body;
                template.UpdatedAtUtc = _clock.UtcNow;
                template.UpdatedByUserId = actorUserId;
                AddAudit(actorUserId, "NotificationTemplateUpdated", "NotificationTemplate", templateId, template.Name, "Updated notification template.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task RecordTestEmailSentAsync(
        Guid tenantId,
        Guid actorUserId,
        string recipientEmail,
        string providerMessageId,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            AddAudit(
                actorUserId,
                "NotificationTestEmailSent",
                "NotificationTestEmail",
                null,
                "Notification test email",
                $"Sent test email to {recipientEmail}.",
                "Admin Center",
                metadataJson);
        }

        return Task.CompletedTask;
    }

    public Task RecordRealtimeTestNotificationSentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid notificationId,
        int connectedClientCount,
        string metadataJson,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            AddAudit(
                actorUserId,
                "NotificationRealtimeTestSent",
                "RealtimeNotification",
                notificationId,
                "Realtime test notification",
                $"Sent realtime test notification to {connectedClientCount} connected client(s).",
                "Admin Center",
                metadataJson);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PersistedRealtimeNotification>> InsertForUsersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> recipientUserIds,
        RealtimeNotificationMessage notification,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var eventId = EnsureRealtimeNotificationEvent(tenantId);
            var persisted = recipientUserIds
                .Where(userId => userId != Guid.Empty)
                .Distinct()
                .Select(userId =>
                {
                    var notificationId = Guid.NewGuid();
                    _notificationRecipients.Add(new NotificationRecipientState
                    {
                        NotificationRecipientId = notificationId,
                        TenantId = tenantId,
                        NotificationEventId = eventId,
                        RecipientUserId = userId,
                        Title = notification.Title,
                        Message = notification.Message,
                        Category = notification.Category,
                        Severity = notification.Severity,
                        EntityType = notification.EntityType,
                        EntityId = Guid.TryParse(notification.EntityId, out var entityId) ? entityId : null,
                        MetadataJson = JsonSerializer.Serialize(notification.Metadata),
                        CreatedAtUtc = notification.CreatedAtUtc
                    });

                    return new PersistedRealtimeNotification(userId, notificationId);
                })
                .ToArray();

            return Task.FromResult<IReadOnlyList<PersistedRealtimeNotification>>(persisted);
        }
    }

    public Task UpdateEventStatusAsync(Guid tenantId, Guid actorUserId, Guid eventId, string status, string metadataJson, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var item = _notificationEvents.FirstOrDefault(item => item.TenantId == tenantId && item.EventId == eventId);
            if (item is not null)
            {
                item.Status = status;
                item.UpdatedAtUtc = _clock.UtcNow;
                AddAudit(actorUserId, "NotificationEventStatusUpdated", "NotificationEvent", eventId, item.Name, $"Changed notification event status to {status}.", "Admin Center", metadataJson);
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var pending = _outbox
                .Where(item => item.Status == "Pending")
                .OrderBy(item => item.CreatedAtUtc)
                .Take(batchSize)
                .ToArray();

            foreach (var item in pending)
            {
                item.Status = "Sent";
                item.AttemptCount++;
                item.ProcessedAtUtc = _clock.UtcNow;
            }

            return Task.FromResult(pending.Length);
        }
    }

    public Task RecordHeartbeatAsync(NotificationWorkerHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _notificationWorkerHeartbeat = heartbeat;
            _notificationWorkerLastHeartbeatUtc = _clock.UtcNow;
            if (heartbeat.LastProcessedCount > 0)
            {
                _notificationWorkerLastProcessedAtUtc = _notificationWorkerLastHeartbeatUtc;
            }
        }

        return Task.CompletedTask;
    }

    private AdminNotificationWorkerStatus BuildNotificationWorkerStatus(Guid tenantId)
    {
        var pendingDueCount = _outbox.Count(item => item.TenantId == tenantId && item.Status == "Pending");
        var processingCount = _outbox.Count(item => item.TenantId == tenantId && item.Status == "Processing");

        string state;
        string label;
        string message;
        var ageSeconds = _notificationWorkerLastHeartbeatUtc.HasValue
            ? (int)Math.Max(0, (_clock.UtcNow - _notificationWorkerLastHeartbeatUtc.Value).TotalSeconds)
            : (int?)null;

        if (_notificationWorkerHeartbeat is null || !_notificationWorkerLastHeartbeatUtc.HasValue)
        {
            state = "Offline";
            label = "Not reporting";
            message = pendingDueCount > 0
                ? "No worker heartbeat has been recorded. Due emails will stay pending until TalentPilot.Worker is started."
                : "No worker heartbeat has been recorded. Start TalentPilot.Worker before expecting queued email delivery.";
        }
        else if (ageSeconds.GetValueOrDefault(int.MaxValue) > NotificationWorkerStaleAfterSeconds)
        {
            var heartbeatAgeSeconds = ageSeconds.GetValueOrDefault();
            state = "Offline";
            label = "Stale heartbeat";
            message = $"Last worker heartbeat was {FormatNotificationWorkerAge(heartbeatAgeSeconds)} ago. Pending emails will stay queued until TalentPilot.Worker is running again.";
        }
        else if (!string.IsNullOrWhiteSpace(_notificationWorkerHeartbeat.LastError))
        {
            state = "Error";
            label = "Running with error";
            message = $"The worker heartbeat is current, but the latest loop reported an error: {_notificationWorkerHeartbeat.LastError}";
        }
        else
        {
            state = "Running";
            label = "Running";
            message = pendingDueCount > 0
                ? $"The worker heartbeat is current. {pendingDueCount} due email(s) are waiting for the next {NotificationWorkerPollIntervalSeconds}-second polling cycle."
                : "The worker heartbeat is current and there are no due pending emails.";
        }

        return new AdminNotificationWorkerStatus(
            state,
            label,
            message,
            _notificationWorkerLastHeartbeatUtc,
            _notificationWorkerHeartbeat?.StartedAtUtc,
            _notificationWorkerLastProcessedAtUtc,
            _notificationWorkerHeartbeat?.LastProcessedCount,
            _notificationWorkerHeartbeat?.HostName,
            _notificationWorkerHeartbeat?.ProcessId,
            _notificationWorkerHeartbeat?.LastError,
            NotificationWorkerPollIntervalSeconds,
            NotificationWorkerStaleAfterSeconds,
            pendingDueCount,
            processingCount);
    }

    private static string FormatNotificationWorkerAge(int seconds)
    {
        if (seconds < 60)
        {
            return $"{Math.Max(0, seconds)} second(s)";
        }

        var minutes = seconds / 60;
        if (minutes < 60)
        {
            return $"{minutes} minute(s)";
        }

        return $"{minutes / 60} hour(s)";
    }

    public Task<AdminAuditLogListResponse> ListAsync(Guid tenantId, AdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var logs = _auditLogs.Where(log => log.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(query.Area))
            {
                logs = logs.Where(log => log.Area.Equals(query.Area, StringComparison.OrdinalIgnoreCase));
            }

            if (query.ActorId.HasValue)
            {
                logs = logs.Where(log => log.ActorUserId == query.ActorId);
            }

            if (!string.IsNullOrWhiteSpace(query.EntityType))
            {
                logs = logs.Where(log => log.EntityType.Equals(query.EntityType, StringComparison.OrdinalIgnoreCase));
            }

            if (query.EntityId.HasValue)
            {
                logs = logs.Where(log => log.EntityId == query.EntityId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                logs = logs.Where(log =>
                    log.EventSummary.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                    log.RecordLabel.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            var materialized = logs.OrderByDescending(log => log.OccurredAtUtc).ToList();
            var items = materialized
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(log => new AdminAuditLogListItem(
                    log.AuditLogId,
                    log.OccurredAtUtc,
                    log.ActorDisplayName,
                    log.EventSummary,
                    log.RecordLabel,
                    log.Area))
                .ToArray();

            var today = _clock.UtcNow.Date;
            var summary = new AdminAuditLogSummary(
                _auditLogs.Count(log => log.TenantId == tenantId && log.OccurredAtUtc.UtcDateTime.Date == today),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "Admin Center"),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "Workflow"),
                _auditLogs.Count(log => log.TenantId == tenantId && log.Area == "AI"));

            return Task.FromResult(new AdminAuditLogListResponse(summary, items, query.Page, query.PageSize, materialized.Count));
        }
    }

    Task<AdminAuditLogDetails?> IAdminAuditLogRepository.GetAsync(Guid tenantId, Guid auditLogId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var log = _auditLogs.FirstOrDefault(item => item.TenantId == tenantId && item.AuditLogId == auditLogId);
            return Task.FromResult(log is null ? null : new AdminAuditLogDetails(
                log.AuditLogId,
                log.OccurredAtUtc,
                log.ActorUserId,
                log.ActorDisplayName,
                log.EventType,
                log.EntityType,
                log.EntityId,
                log.RecordLabel,
                log.EventSummary,
                log.Area,
                log.MetadataJson));
        }
    }

    private void SeedPermissions()
    {
        _permissions.AddRange([
            new PermissionCatalogItem(AccessConstants.ManageAdminCenter, "Manage Admin Center", "Administration", "Configure tenant, roles, groups, and settings.", "Active"),
            new PermissionCatalogItem(AccessConstants.ManageUsers, "Manage Users", "Administration", "Create and maintain internal users.", "Active"),
            new PermissionCatalogItem(AccessConstants.ManageRoles, "Manage Roles", "Administration", "Maintain tenant roles and permission policy.", "Active"),
            new PermissionCatalogItem(AccessConstants.ViewAuditLogs, "View Audit Logs", "Governance", "Review audit events stored in UTC.", "Active"),
            new PermissionCatalogItem(AccessConstants.CreateJobRequest, "Create Job Request", "Recruitment", "Create presales resource requests.", "Active"),
            new PermissionCatalogItem(AccessConstants.ViewJobRequests, "View Job Requests", "Recruitment", "View job requests and assigned work.", "Active"),
            new PermissionCatalogItem(AccessConstants.ClaimWorkflowTask, "Claim Workflow Task", "Recruitment", "Claim assigned recruitment workflow tasks.", "Active"),
            new PermissionCatalogItem("candidates.manage", "Manage Candidates", "Recruitment", "Manage candidates and applications.", "Active"),
            new PermissionCatalogItem(AccessConstants.ManageInterviews, "Manage Interviews", "Recruitment", "Schedule interviews and submit interview feedback.", "Active")
        ]);
    }

    private void SeedRoles()
    {
        var tenantAdmin = AddRole(AccessConstants.TenantAdminRoleCode, "Tenant Admin", "Tenant", "Tenant", 1, false, true, [
            AccessConstants.ManageAdminCenter,
            AccessConstants.ManageUsers,
            AccessConstants.ManageRoles,
            AccessConstants.ViewAuditLogs
        ]);
        AddRole("Presales", "Presales", "Tenant", "Tenant", 10, false, true, [
            AccessConstants.CreateJobRequest,
            AccessConstants.ViewJobRequests
        ]);
        var pmo = AddRole(AccessConstants.PmoRoleCode, "PMO", "Tenant", "Tenant", 20, false, true, [
            AccessConstants.CreateJobRequest,
            AccessConstants.ViewJobRequests,
            AccessConstants.ClaimWorkflowTask
        ]);
        AddRole("Recruiter", "Recruiter", "Tenant", "Tenant", 30, false, true, [
            AccessConstants.ViewJobRequests,
            AccessConstants.ClaimWorkflowTask,
            "candidates.manage"
        ]);
        AddRole("HiringManager", "Hiring Manager", "Tenant", "Tenant", 40, false, true, [
            AccessConstants.ViewJobRequests
        ]);
        AddRole(AccessConstants.HodRoleCode, "HOD / Department Head", "Tenant", "Tenant", 45, false, true, [
            AccessConstants.ManageInterviews
        ]);
        AddRole("Interviewer", "Interviewer", "Tenant", "Tenant", 50, false, true, [
            AccessConstants.ManageInterviews
        ]);
        AddRole("Employee", "Employee", "Tenant", "Tenant", 90, false, true, [
            AccessConstants.ViewJobRequests
        ]);
        AddRole("Candidate", "Candidate", "Tenant", "Tenant", 100, false, false, []);

        _benchVisibilityRoleId = pmo.RoleId;
        _ = tenantAdmin;
    }

    private void SeedGroups()
    {
        AddGroup("Admin Team", "Tenant administration");
        AddGroup("PMO - Engineering", "Workflow routing: Engineering intake");
        AddGroup("PMO - QA", "Workflow routing: QA intake");
        AddGroup("PMO - DevOps", "Workflow routing: DevOps intake");
        AddGroup("Recruiting - Delivery", "Workflow routing: recruiting handoff");
        AddGroup("Delivery Leadership", "Final hiring-manager review");
        AddGroup("Interview Panel - Engineering", "Interview assignments");
        AddGroup("Engineering", "Employee department grouping");
    }

    private void SeedSkills()
    {
        AddSkill("Angular", "Frontend", ["Angular 17", "Angular 18"]);
        AddSkill(".NET", "Backend", ["ASP.NET Core", "C#"]);
        AddSkill("SQL Server", "Database", ["T-SQL", "Microsoft SQL Server"]);
        AddSkill("Azure", "Cloud", ["Azure App Service", "Azure SQL"]);
        AddSkill("React", "Frontend", ["React.js"]);
        AddSkill("QA Automation", "Quality", ["Selenium", "Playwright"]);
        AddSkill("DevOps", "Platform", ["CI/CD", "Docker"]);
        AddSkill("Python", "Backend", ["FastAPI"]);
        AddSkill("AI/ML", "Data", ["LLM", "Embeddings"]);

        foreach (var (category, skills) in ExpandedSkillCatalog)
        {
            AddSkillCatalogGroup(category, skills);
        }
    }

    private void SeedUsers()
    {
        var tenantAdmin = FindRoleByCode(AccessConstants.TenantAdminRoleCode).RoleId;
        var presales = FindRoleByCode("Presales").RoleId;
        var pmo = FindRoleByCode(AccessConstants.PmoRoleCode).RoleId;
        var recruiter = FindRoleByCode("Recruiter").RoleId;
        var hiringManager = FindRoleByCode("HiringManager").RoleId;
        var hod = FindRoleByCode(AccessConstants.HodRoleCode).RoleId;
        var interviewer = FindRoleByCode("Interviewer").RoleId;
        var employee = FindRoleByCode("Employee").RoleId;
        var candidate = FindRoleByCode("Candidate").RoleId;

        AddUser("11111111-1111-1111-1111-111111111111", "Mudasar Ahmad", "admin@tkxel.com", [tenantAdmin], ["Admin Team"], "Administration", _clock.UtcNow.AddMinutes(-70), 9.0m, joiningDate: new DateOnly(2017, 8, 14));
        AddUser("22222222-2222-2222-2222-222222222222", "Ahmed Raza", "ai-presales@8pkk57.onmicrosoft.com", [presales], ["Presales Team"], "Presales", _clock.UtcNow.AddMinutes(-30), 7.0m, joiningDate: new DateOnly(2019, 3, 11));
        AddUser("33333333-3333-3333-3333-333333333333", "Ali Khan", "ai-pmo@8pkk57.onmicrosoft.com", [pmo], ["PMO - Engineering", "PMO - QA", "PMO - DevOps"], "PMO", _clock.UtcNow.AddDays(-1), 8.0m, joiningDate: new DateOnly(2018, 6, 4));
        AddUser("44444444-4444-4444-4444-444444444444", "Sara Malik", "ai-recruiter@8pkk57.onmicrosoft.com", [recruiter], ["Recruiting - Delivery"], "Recruitment", _clock.UtcNow.AddHours(-2), 5.0m, joiningDate: new DateOnly(2021, 1, 18));
        AddUser("55555555-5555-5555-5555-555555555555", "Fatima Noor", "ai-hiring.manager@8pkk57.onmicrosoft.com", [hiringManager], ["Delivery Leadership"], "Engineering", _clock.UtcNow.AddMinutes(-90), 10.0m, 12, new DateOnly(2016, 9, 5));
        AddUser("99999999-9999-9999-9999-999999999999", "Zara Siddiqui", "ai-hod.engineering@8pkk57.onmicrosoft.com", [hod], ["Interview Panel - Engineering", "Engineering"], "Engineering", _clock.UtcNow.AddMinutes(-45), 13.0m, 24, new DateOnly(2014, 2, 10));
        AddUser("66666666-6666-6666-6666-666666666666", "Bilal Hussain", "ai-interviewer@8pkk57.onmicrosoft.com", [interviewer], ["Interview Panel - Engineering"], "Engineering", _clock.UtcNow.AddDays(-3), 6.0m, 18, new DateOnly(2020, 4, 20));
        AddUser("77777777-7777-7777-7777-777777777777", "Bench Employee", "employee@tkxel.com", [employee], ["Engineering"], "Engineering", _clock.UtcNow.AddDays(-5), 4.0m, joiningDate: new DateOnly(2022, 2, 7));
        AddUser("88888888-8888-8888-8888-888888888888", "Ayesha Khan", "ai-candidate@8pkk57.onmicrosoft.com", [candidate], [], "Candidate", _clock.UtcNow.AddDays(-2));
    }

    private void SeedDepartments()
    {
        AddDepartment("ADMIN", "Administration", "admin@tkxel.com");
        AddDepartment("PRESALES", "Presales", "ai-presales@8pkk57.onmicrosoft.com");
        AddDepartment("PMO", "PMO", "ai-pmo@8pkk57.onmicrosoft.com");
        AddDepartment("RECRUITMENT", "Recruitment", "ai-recruiter@8pkk57.onmicrosoft.com");
        AddDepartment("ENG", "Engineering", "ai-hod.engineering@8pkk57.onmicrosoft.com");
        AddDepartment("QA", "QA", "ai-interviewer@8pkk57.onmicrosoft.com");
        AddDepartment("DEVOPS", "DevOps", "ai-interviewer@8pkk57.onmicrosoft.com");
    }

    private void SeedIntakeRoutingRules()
    {
        foreach (var mapping in new[]
                 {
                     ("Engineering", "PMO - Engineering"),
                     ("QA", "PMO - QA"),
                     ("DevOps", "PMO - DevOps")
                 })
        {
            var department = _departments.First(item => item.Name == mapping.Item1);
            var pmoGroupId = _groups.First(group => group.Name == mapping.Item2).GroupId;
            _intakeRoutingRules.Add(new IntakeRoutingRuleState
            {
                JobRequestIntakeRoutingRuleId = Guid.NewGuid(),
                TenantId = TenantId,
                DepartmentId = department.DepartmentId,
                AssignmentType = "Group",
                TargetGroupId = pmoGroupId,
                Status = "Active"
            });
        }
    }

    private void SeedCandidateSourceLabels()
    {
        AddCandidateSourceLabel("LinkedInManual", "LinkedIn", "External sourcing");
        AddCandidateSourceLabel("IndeedManual", "Indeed", "External sourcing");
        AddCandidateSourceLabel("Referral", "Referral", "Referral reporting");
        AddCandidateSourceLabel("Other", "Other", "Manual review");
    }

    private void SeedInterviewTemplates()
    {
        var templateId = Guid.Parse("99999999-aaaa-bbbb-cccc-000000000401");
        var engineeringDepartmentId = _departments.First(department => department.Name == "Engineering").DepartmentId;
        var recruiterRoleId = FindRoleByCode("Recruiter").RoleId;
        var interviewerRoleId = FindRoleByCode("Interviewer").RoleId;
        var hodRoleId = FindRoleByCode(AccessConstants.HodRoleCode).RoleId;
        var recruiterUserId = _users.First(user => user.Email.Equals("ai-recruiter@8pkk57.onmicrosoft.com", StringComparison.OrdinalIgnoreCase)).UserId;
        var interviewerUserId = _users.First(user => user.Email.Equals("ai-interviewer@8pkk57.onmicrosoft.com", StringComparison.OrdinalIgnoreCase)).UserId;
        var departmentHeadUserId = _users.First(user => user.Email.Equals("ai-hod.engineering@8pkk57.onmicrosoft.com", StringComparison.OrdinalIgnoreCase)).UserId;

        _interviewTemplates.Add(new InterviewTemplateState
        {
            InterviewTemplateId = templateId,
            TenantId = TenantId,
            DepartmentId = engineeringDepartmentId,
            Name = "Senior Software Engineer Interview",
            Description = "Starter interview template recruiters can copy and customize per job post.",
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow
        });
        AddInterviewTemplateRound(templateId, 1, "HR Screening", recruiterRoleId, recruiterUserId, 30);
        AddInterviewTemplateRound(templateId, 2, "Technical Interview", interviewerRoleId, interviewerUserId, 60);
        AddInterviewTemplateRound(templateId, 3, "Department Head Interview", hodRoleId, departmentHeadUserId, 45);
    }

    private void SeedNotifications()
    {
        AddNotification(NotificationEventCodes.PresalesRequestSubmitted, "Presales request submitted", "DepartmentIntakeRoute", "PMO intake email", "Configured department intake recipient", "New request: {{jobTitle}}", "{{requesterName}} submitted {{jobTitle}} for PMO review.", ["jobTitle", "requesterName"]);
        AddNotification(NotificationEventCodes.PmoEmployeeReferred, "PMO referred employee", "User:PresalesOwner", "Employee referral email", "Presales Owner", "PMO referred {{employeeName}}", "{{employeeName}} was referred for {{jobTitle}}. Review the recommendation in Talent Pilot.", ["employeeName", "jobTitle"]);
        AddNotification(NotificationEventCodes.PmoForwardedToRecruiting, "PMO forwarded to recruiting", "Group:Recruiting", "Recruiting handoff email", "Recruiting - Delivery", "Recruiting handoff: {{jobTitle}}", "PMO forwarded {{jobTitle}} to recruiting after bench review.", ["jobTitle"]);
        AddNotification(NotificationEventCodes.PresalesEmployeeReferralAccepted, "Presales accepted employee referral", "User:PMOReferralOwner", "Accepted referral email", "PMO Referral Owner", "Presales accepted an internal employee for {{jobTitle}}", "{{requesterName}} accepted {{acceptedCount}} internal employee recommendation(s) and rejected {{rejectedCount}} for {{jobTitle}}.", ["requesterName", "acceptedCount", "rejectedCount", "jobTitle"]);
        AddNotification(NotificationEventCodes.PresalesEmployeeReferralRejected, "Presales rejected employee referral", "User:PMOReferralOwner", "Rejected referral email", "PMO Referral Owner", "Presales rejected internal recommendations for {{jobTitle}}", "{{requesterName}} rejected {{rejectedCount}} internal employee recommendation(s) for {{jobTitle}}. The request has returned to PMO Review.", ["requesterName", "rejectedCount", "jobTitle"]);
        AddNotification(NotificationEventCodes.RecruiterAssignedInterviewers, "Recruiter assigned interviewers", "User:Interviewer", "Interview assignment email", "Interviewer", "Interview assigned: {{candidateName}}", "You have been assigned to interview {{candidateName}} for {{jobTitle}}.", ["candidateName", "jobTitle"]);
        AddNotification(NotificationEventCodes.InterviewScheduled, "Interview scheduled", "User:InterviewParticipants", "Interview scheduled email", "Candidate, Interviewer, Hiring Manager", "Interview scheduled: {{jobTitle}}", "Interview scheduling emails are generated by backend code with candidate, interviewer, hiring manager, date, duration, and meeting details.", ["jobTitle", "candidateName", "roundName", "startsAtUtc", "durationMinutes", "meetingLink", "locationText"]);
        AddNotification(NotificationEventCodes.InterviewFeedbackSubmitted, "Interview feedback submitted", "User:Recruiter", "Feedback received email", "Recruiter", "Feedback submitted for {{candidateName}}", "Interview feedback for {{candidateName}} is ready for recruiter review.", ["candidateName"]);
        AddNotification(NotificationEventCodes.CandidateStageChanged, "Candidate stage changed", "User:CandidateOrOwner", "Candidate stage email", "Candidate or Owner", "Application update: {{stageName}}", "{{candidateName}} moved to {{stageName}} for {{jobTitle}}.", ["candidateName", "stageName", "jobTitle"]);
        AddNotification(NotificationEventCodes.HiringManagerReviewReady, "Hiring manager review ready", "User:HiringManager", "Hiring manager review email", "Hiring Manager", "Final review ready: {{candidateName}}", "{{candidateName}} is ready for final hiring-manager review for {{jobTitle}}.", ["candidateName", "jobTitle"]);
    }

    private void SeedAiAgents()
    {
        AddAiAgent("requirement-parser", "Requirement Parser", "Builds the saved requirement profile used for future semantic matching when a Job Request is created.", "Controlled Job Request intake fields, final saved description, department, location, skills, experience range, positions, and priority.", "Indexed requirement profile and embedding metadata for downstream agents.", "Runs after save; it cannot approve, reject, or move workflow stages.");
        AddAiAgent("job-description-drafter", "Job Description Drafter", "Drafts editable Job Request descriptions from controlled intake fields.", "Job title, client, department, location, selected tenant skills, experience range, required positions, priority, and hiring manager.", "Plain-text job description ready for human editing.", "Human review is required before save; the agent cannot approve, reject, or move workflow stages.");
        AddAiAgent("cv-parser", "CV Parser", "Prefills recruiter manual sourcing forms from DOCX resumes.", "DOCX text extracted server-side from the Add Candidate flow.", "Structured candidate contact, profile, education, experience, and skill evidence for recruiter review.", "DOCX only for MVP; recruiters review and edit every extracted field before inviting.");
        AddAiAgent("bench-matching", "Bench Matching", "Ranks eligible internal employees for PMO Review using skill coverage, vector similarity, experience, location, availability, project evidence, and optional Tavily web research capped at 60 requests per day.", "Claimed PMO Review request, required skills, saved job description embedding, active benched employees, employee skills, employee locations, project assignments, and safe public client/project snippets when available.", "Ranked employee matches with score, confidence, strengths, gaps, location fit, project evidence, web research status, and fit rationale.", "PMO decides whether to refer an employee; the agent cannot recommend directly to Presales or move workflow stages.");
        AddAiAgent("talent-rediscovery", "Talent Rediscovery", "Ranks previous warm candidates before external sourcing using candidate skills, historical applications, interview feedback, outcomes, and vector similarity. No web search is used for candidate data.", "Claimed Recruiter Sourcing request or draft Job Post, active tenant candidates with useful historical applications, candidate skills, interview feedback, prior outcomes, and candidate profile embeddings.", "Ranked warm candidates with score, confidence, matched skills, gaps, prior application evidence, interview evidence, and caveats.", "Recruiters review the ranking manually; the agent cannot contact candidates, move workflow stages, or make hiring decisions.");
        AddAiAgent("applicant-ranking", "Applicant Ranking", "Ranks current applications for an active job post using candidate profile data, application evidence, uploaded CV/cover-letter context, interview history, and vector similarity. No web search is used.", "Claimed Recruiter Sourcing job post, current active job-post applications, candidate skills/profile fields, cover letter, uploaded application documents, historical applications, interview feedback, and application/job post embeddings.", "Ranked current applications with deterministic score, confidence, matched skills, gaps, document evidence, historical outcome evidence, semantic similarity status, and recruiter-facing rationale.", "Recruiters decide whether to shortlist, schedule, hold, reject, or forward. The agent cannot contact candidates or move workflow stages.");
        AddAiAgent("interview-question-recommender", "Interview Question Recommender", "Recommends interviewer-facing questions for HR, screening, technical, HOD, and behavioral rounds using interview context, seeded question-bank retrieval, vector ranking, and LLM-generated structured output.", "Assigned interview task, job request/post details, required skills, candidate profile, cover letter, application document excerpts, prior interview evidence, and retrieved question-bank items.", "Natural-language summary plus structured question objects with rationale, expected signal, follow-ups, rubric, source bank item, coverage, model, prompt version, and run metadata.", "Advisory only. Interviewers own final assessment; the agent cannot submit feedback, hire, reject, schedule, contact candidates, or move workflow stages.");
        AddAiAgent("fit-explanation", "Fit Explanation", "Explains why an employee or candidate was ranked in Bench Matching or Talent Rediscovery.", "Recommendation evidence, skills, experience, location, project/application history, interview evidence, and gaps.", "Readable strengths, gaps, confidence notes, and caveats embedded in the ranking result.", "Explanation supports human review only and never selects or contacts candidates by itself.");
        AddAiAgent("hiring-manager-decision-brief", "Hiring Manager Decision Brief", "Summarizes interview feedback and candidate context on Hiring Manager Review.", "Candidate profile, source details, recruiter notes, job request/post summary, interview statuses, scores, recommendations, and skipped-round reasons.", "Advisory decision brief shown to the Hiring Manager before offer or final outcome actions.", "Hiring Manager owns the final decision; the brief cannot generate offers or close requests by itself.");
    }

    private void SeedAuditLogs()
    {
        AddAudit(SystemActorId, "UserRoleAssigned", "User", _users[2].UserId, "PMO user", "Updated PMO user role assignments.", "Admin Center", "{}");
        AddAudit(_users[2].UserId, "BenchEmployeeProposed", "JobRequest", Guid.NewGuid(), "Bench referral", "Proposed bench employee for request.", "Workflow", "{}");
        AddAudit(SystemActorId, "RequirementExtracted", "JobRequest", Guid.NewGuid(), "Job request", "Completed requirement extraction.", "AI", "{}");
        AddAudit(_users[3].UserId, "CandidateInviteCreated", "CandidateInvite", Guid.NewGuid(), "Candidate invite", "Created candidate invite link.", "Talent Pilot App", "{}");
    }

    private Guid EnsureRealtimeNotificationEvent(Guid tenantId)
    {
        var existing = _notificationEvents.FirstOrDefault(item =>
            item.TenantId == tenantId &&
            item.EventCode.Equals("REALTIME_NOTIFICATION", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing.EventId;
        }

        var eventId = Guid.NewGuid();
        _notificationEvents.Add(new NotificationEventState(
            eventId,
            tenantId,
            "REALTIME_NOTIFICATION",
            "Realtime notification",
            "Realtime",
            "Active",
            _clock.UtcNow));

        return eventId;
    }

    private RoleState AddRole(string code, string name, string type, string scope, int priority, bool isProtected, bool isBulkAssignable, IReadOnlyList<string> permissionIds)
    {
        var role = new RoleState
        {
            RoleId = Guid.NewGuid(),
            TenantId = TenantId,
            Code = code,
            Name = name,
            Type = type,
            Scope = scope,
            Priority = priority,
            Status = "Active",
            IsProtected = isProtected,
            IsBulkAssignable = isBulkAssignable,
            PermissionIds = permissionIds.ToList()
        };
        _roles.Add(role);
        return role;
    }

    private static IReadOnlyList<WorkflowActionDefinition> WorkflowActions() =>
    [
        new(TransitionCreateByPresalesId, "CREATE_BY_PRESALES", "Create by Presales"),
        new(TransitionRecommendEmployeesId, "RECOMMEND_EMPLOYEES_TO_PRESALES", "Recommend Employees to Presales"),
        new(TransitionPresalesReturnPmoId, "PRESALES_RETURN_TO_PMO", "Presales Return to PMO"),
        new(TransitionPresalesAcceptInternalId, "PRESALES_ACCEPT_INTERNAL_EMPLOYEE", "Presales Accept Internal Employee"),
        new(TransitionForwardRecruiterId, "FORWARD_TO_RECRUITER", "Forward to Recruiter"),
        new(TransitionInterviewId, "MOVE_TO_INTERVIEWING", "Move to Interviewing"),
        new(TransitionHiringManagerId, "FORWARD_TO_HIRING_MANAGER", "Forward to Hiring Manager")
    ];

    private static WorkflowActionDefinition WorkflowAction(Guid workflowTransitionId) =>
        WorkflowActions().First(action => action.WorkflowTransitionId == workflowTransitionId);

    private void AddGroup(string name, string purpose)
    {
        _groups.Add(new GroupState
        {
            GroupId = Guid.NewGuid(),
            TenantId = TenantId,
            Name = name,
            Purpose = purpose,
            Status = "Active"
        });
    }

    private void AddSkill(string name, string category, IReadOnlyList<string> aliases)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        if (_skills.Any(skill => skill.NormalizedName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _skills.Add(new SkillState
        {
            SkillId = Guid.NewGuid(),
            TenantId = TenantId,
            Name = name,
            NormalizedName = normalizedName,
            Category = category,
            Aliases = aliases.ToList(),
            IsVectorRelevant = true,
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow
        });
    }

    private void AddSkillCatalogGroup(string category, string skillList)
    {
        foreach (var skillName in skillList.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddSkill(skillName, category, []);
        }
    }

    private void AddDepartment(string code, string name, string leadEmail)
    {
        _departments.Add(new DepartmentState
        {
            DepartmentId = DepartmentIdFromName(name),
            TenantId = TenantId,
            Code = code,
            Name = name,
            LeadUserId = _users.FirstOrDefault(user => user.Email.Equals(leadEmail, StringComparison.OrdinalIgnoreCase))?.UserId,
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow
        });
    }

    private void AddCandidateSourceLabel(string code, string displayName, string reportingCategory)
    {
        _candidateSourceLabels.Add(new CandidateSourceLabelState
        {
            CandidateSourceLabelId = Guid.NewGuid(),
            TenantId = TenantId,
            Code = code,
            DisplayName = displayName,
            ReportingCategory = reportingCategory,
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow
        });
    }

    private void AddInterviewTemplateRound(
        Guid templateId,
        int roundOrder,
        string name,
        Guid ownerRoleId,
        Guid ownerUserId,
        int durationMinutes)
    {
        _interviewTemplateRounds.Add(new InterviewTemplateRoundState
        {
            InterviewTemplateRoundId = Guid.NewGuid(),
            TenantId = TenantId,
            InterviewTemplateId = templateId,
            RoundOrder = roundOrder,
            Name = name,
            OwnerRoleId = ownerRoleId,
            OwnerUserId = ownerUserId,
            DurationMinutes = durationMinutes,
            IsRequired = true,
            Status = "Active"
        });
    }

    private void AddUser(
        string id,
        string displayName,
        string email,
        IReadOnlyList<Guid> roleIds,
        IReadOnlyList<string> groupNames,
        string departmentName,
        DateTimeOffset lastActiveAtUtc,
        decimal? experienceYears = null,
        int completedInterviewCount = 0,
        DateOnly? joiningDate = null)
    {
        var groupIds = _groups
            .Where(group => groupNames.Contains(group.Name, StringComparer.OrdinalIgnoreCase))
            .Select(group => group.GroupId)
            .ToList();

        _users.Add(new UserState
        {
            UserId = Guid.Parse(id),
            TenantId = TenantId,
            DisplayName = displayName,
            Email = email,
            Initials = BuildInitials(displayName),
            AccountStatus = "Active",
            PasswordHash = DemoPasswordHash,
            RoleIds = roleIds.ToList(),
            GroupIds = groupIds,
            DepartmentName = departmentName,
            ExperienceYears = experienceYears,
            JoiningDate = joiningDate,
            CompletedInterviewCount = completedInterviewCount,
            LastActiveAtUtc = lastActiveAtUtc,
            CreatedAtUtc = _clock.UtcNow.AddDays(-10),
            UpdatedAtUtc = _clock.UtcNow.AddDays(-1)
        });
    }

    private void AddNotification(
        string eventCode,
        string eventName,
        string defaultRecipientType,
        string templateName,
        string templateRecipient,
        string subject,
        string body,
        IReadOnlyList<string> variables)
    {
        _notificationEvents.Add(new NotificationEventState(Guid.NewGuid(), TenantId, eventCode, eventName, defaultRecipientType, "Active", _clock.UtcNow));
        _notificationTemplates.Add(new NotificationTemplateState
        {
            TemplateId = Guid.NewGuid(),
            TenantId = TenantId,
            EventCode = eventCode,
            Name = templateName,
            Recipient = templateRecipient,
            Subject = subject,
            Body = body,
            Variables = variables.ToList(),
            Status = "Active",
            UpdatedAtUtc = _clock.UtcNow,
            UpdatedByUserId = SystemActorId
        });
    }

    private void AddAiAgent(
        string id,
        string displayName,
        string responsibility,
        string inputSummary,
        string outputSummary,
        string mvpBoundary)
    {
        _aiAgents.Add(new AiAgentDefinitionState
        {
            Id = id,
            DisplayName = displayName,
            Responsibility = responsibility,
            InputSummary = inputSummary,
            OutputSummary = outputSummary,
            MvpBoundary = mvpBoundary,
            Enabled = true
        });
    }

    private void AddAudit(Guid actorUserId, string eventType, string entityType, Guid? entityId, string recordLabel, string eventSummary, string area, string metadataJson)
    {
        var actor = _users.FirstOrDefault(user => user.UserId == actorUserId)?.DisplayName ?? "System";
        _auditLogs.Add(new AuditLogState
        {
            AuditLogId = Guid.NewGuid(),
            TenantId = TenantId,
            OccurredAtUtc = _clock.UtcNow,
            ActorUserId = actorUserId == SystemActorId ? null : actorUserId,
            ActorDisplayName = actor,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            RecordLabel = recordLabel,
            EventSummary = eventSummary,
            Area = area,
            MetadataJson = metadataJson
        });
    }

    private IEnumerable<UserState> FilterUsersForRoleAssignment(Guid tenantId, RoleUserAssignmentFilterInput input)
    {
        var users = _users.Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            users = users.Where(user =>
                user.DisplayName.Contains(input.Search, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(input.Search, StringComparison.OrdinalIgnoreCase) ||
                user.DepartmentName.Contains(input.Search, StringComparison.OrdinalIgnoreCase));
        }

        if (input.AccountStatuses is { Count: > 0 })
        {
            users = users.Where(user => input.AccountStatuses.Contains(user.AccountStatus, StringComparer.OrdinalIgnoreCase));
        }

        if (input.CurrentRoleIds is { Count: > 0 })
        {
            users = users.Where(user => user.RoleIds.Any(input.CurrentRoleIds.Contains));
        }

        if (input.GroupIds is { Count: > 0 })
        {
            users = users.Where(user => user.GroupIds.Any(input.GroupIds.Contains));
        }

        return users.OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private AuthUserRecord ToAuthUserRecord(UserState user)
    {
        return new AuthUserRecord
        {
            UserId = user.UserId,
            TenantId = user.TenantId,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AccountStatus = user.AccountStatus,
            PasswordHash = user.PasswordHash
        };
    }

    private AdminUserListItem ToUserListItem(UserState user)
    {
        var roles = _roles.Where(role => user.RoleIds.Contains(role.RoleId)).OrderBy(role => role.Priority).ToArray();
        var highestRole = roles.First();
        var groups = _groups.Where(group => user.GroupIds.Contains(group.GroupId)).OrderBy(group => group.Name).ToArray();
        return new AdminUserListItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            roles.Select(role => role.RoleId).ToArray(),
            roles.Select(role => role.Name).ToArray(),
            highestRole.RoleId,
            highestRole.Name,
            highestRole.Priority,
            groups.Select(group => group.GroupId).ToArray(),
            groups.Select(group => group.Name).ToArray(),
            string.IsNullOrWhiteSpace(user.DepartmentName) ? null : DepartmentIdFromName(user.DepartmentName),
            string.IsNullOrWhiteSpace(user.DepartmentName) ? null : user.DepartmentName,
            user.ExperienceYears,
            user.JoiningDate,
            user.CompletedInterviewCount,
            user.AccountStatus,
            user.LastActiveAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private AdminGroupListItem ToGroupListItem(GroupState group)
    {
        return new AdminGroupListItem(
            group.GroupId,
            group.Name,
            group.Purpose,
            group.Status,
            _users.Count(user => user.TenantId == group.TenantId && user.GroupIds.Contains(group.GroupId)));
    }

    private AdminDepartmentListItem ToAdminDepartmentListItem(DepartmentState department)
    {
        var leadName = department.LeadUserId.HasValue
            ? _users.FirstOrDefault(user => user.UserId == department.LeadUserId.Value)?.DisplayName
            : null;

        return new AdminDepartmentListItem(
            department.DepartmentId,
            department.Code,
            department.Name,
            leadName ?? "Unassigned",
            _users.Count(user =>
                user.TenantId == department.TenantId &&
                user.DepartmentName.Equals(department.Name, StringComparison.OrdinalIgnoreCase)),
            department.Name.Equals("Engineering", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            department.Status);
    }

    private static AdminSkillListItem ToAdminSkillListItem(SkillState skill)
    {
        return new AdminSkillListItem(
            skill.SkillId,
            skill.Name,
            skill.NormalizedName,
            skill.Category,
            skill.Aliases.ToArray(),
            skill.Status,
            skill.UpdatedAtUtc);
    }

    private AdminHiringPipelineTemplateItem ToHiringPipelineTemplateItem(InterviewTemplateState template)
    {
        var activeRounds = _interviewTemplateRounds
            .Where(round =>
                round.TenantId == template.TenantId &&
                round.InterviewTemplateId == template.InterviewTemplateId &&
                round.Status == "Active")
            .OrderBy(round => round.RoundOrder)
            .ToArray();
        var stageFlow = activeRounds.Length == 0
            ? "No active rounds"
            : string.Join(" -> ", activeRounds.Select(round => round.Name));
        var defaultInterviewers = activeRounds.Length == 0
            ? "Unassigned"
            : string.Join(", ", activeRounds.Select(round => FindUserName(round.OwnerUserId)).Distinct(StringComparer.OrdinalIgnoreCase));

        return new AdminHiringPipelineTemplateItem(
            template.InterviewTemplateId,
            template.Name,
            FindDepartmentName(template.DepartmentId),
            template.Description,
            stageFlow,
            defaultInterviewers,
            activeRounds.Length,
            template.Status,
            template.UpdatedAtUtc);
    }

    private AdminHiringPipelineTemplateDetails ToHiringPipelineTemplateDetails(InterviewTemplateState template)
    {
        var rounds = _interviewTemplateRounds
            .Where(round => round.TenantId == template.TenantId && round.InterviewTemplateId == template.InterviewTemplateId)
            .OrderBy(round => round.RoundOrder)
            .Select(round => new AdminHiringPipelineTemplateRoundItem(
                round.InterviewTemplateRoundId,
                round.RoundOrder,
                round.Name,
                round.OwnerRoleId,
                FindRoleName(round.OwnerRoleId),
                round.OwnerUserId,
                FindUserName(round.OwnerUserId),
                round.DurationMinutes,
                true,
                round.Status))
            .ToArray();

        return new AdminHiringPipelineTemplateDetails(
            template.InterviewTemplateId,
            template.DepartmentId,
            template.Name,
            FindDepartmentName(template.DepartmentId),
            template.Description,
            template.Status,
            template.UpdatedAtUtc,
            rounds);
    }

    private static bool MatchesSkillSearch(SkillState skill, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return skill.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            skill.NormalizedName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            skill.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            skill.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            skill.Aliases.Any(alias => alias.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCandidateSourceSearch(CandidateSourceLabelState source, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return source.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            source.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            source.ReportingCategory.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            source.Status.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesHiringPipelineSearch(AdminHiringPipelineTemplateItem template, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return template.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            template.DepartmentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            template.StageFlow.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            template.DefaultInterviewers.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            template.Status.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private AdminGroupMembershipUser ToGroupMembershipUser(UserState user, Guid groupId)
    {
        var roles = _roles
            .Where(role => user.RoleIds.Contains(role.RoleId))
            .OrderBy(role => role.Priority)
            .Select(role => role.Name)
            .ToArray();

        return new AdminGroupMembershipUser(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            roles,
            user.AccountStatus,
            user.GroupIds.Contains(groupId),
            false);
    }

    private AdminGroupMembershipUser[] BuildGroupMembershipCandidates(Guid tenantId, Guid groupId)
    {
        return _users
            .Where(user => user.TenantId == tenantId && IsInternalUser(user))
            .Select(user => ToGroupMembershipUser(user, groupId))
            .ToArray();
    }

    private static AdminGroupMembershipUser[] ApplyGroupMembershipSearch(
        IReadOnlyCollection<AdminGroupMembershipUser> users,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return users.ToArray();
        }

        var normalized = search.Trim();
        return users
            .Where(user =>
                user.DisplayName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                user.RoleNames.Any(role => role.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ||
                user.AccountStatus.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AdminGroupMembershipUser[] ApplyGroupMembershipFilter(
        IReadOnlyCollection<AdminGroupMembershipUser> users,
        string? membership)
    {
        if (string.Equals(membership, "Members", StringComparison.OrdinalIgnoreCase))
        {
            return users.Where(user => user.IsMember).ToArray();
        }

        if (string.Equals(membership, "Available", StringComparison.OrdinalIgnoreCase))
        {
            return users.Where(user => !user.IsMember).ToArray();
        }

        return users.ToArray();
    }

    private (HashSet<Guid> UserIdsToAdd, HashSet<Guid> UserIdsToRemove) ResolveGroupMembershipChanges(
        Guid tenantId,
        Guid groupId,
        UpdateGroupMembersInput input)
    {
        var explicitAddIds = (input.UserIdsToAdd ?? []).Distinct().ToArray();
        var explicitRemoveIds = (input.UserIdsToRemove ?? []).Distinct().ToArray();
        var userIdsToAdd = new HashSet<Guid>();
        var userIdsToRemove = new HashSet<Guid>();

        if (input.BulkSelection is not null)
        {
            var candidates = BuildGroupMembershipCandidates(tenantId, groupId);
            var searchedUsers = ApplyGroupMembershipSearch(candidates, input.BulkSelection.Search);
            var matchingUsers = ApplyGroupMembershipFilter(searchedUsers, input.BulkSelection.Membership);

            if (string.Equals(input.BulkSelection.Mode, "AddMatching", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var user in matchingUsers.Where(user => !user.IsMember))
                {
                    userIdsToAdd.Add(user.UserId);
                }
            }
            else if (string.Equals(input.BulkSelection.Mode, "RemoveMatching", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var user in matchingUsers.Where(user => user.IsMember))
                {
                    userIdsToRemove.Add(user.UserId);
                }
            }
        }

        foreach (var userId in explicitAddIds)
        {
            userIdsToRemove.Remove(userId);
            userIdsToAdd.Add(userId);
        }

        foreach (var userId in explicitRemoveIds)
        {
            userIdsToAdd.Remove(userId);
            userIdsToRemove.Add(userId);
        }

        return (userIdsToAdd, userIdsToRemove);
    }

    private bool IsInternalUser(UserState user)
    {
        var roles = _roles.Where(role => user.RoleIds.Contains(role.RoleId));
        return roles.All(role => role.Code != "Candidate" && role.Scope != "Portal");
    }

    private AdminUserDetails ToUserDetails(UserState user)
    {
        return new AdminUserDetails(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.Initials,
            user.RoleIds.ToArray(),
            user.GroupIds.ToArray(),
            user.AccountStatus,
            user.LastActiveAtUtc,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private RoleSummary ToRoleSummary(RoleState role)
    {
        return new RoleSummary(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            _users.Count(user => user.RoleIds.Contains(role.RoleId)),
            BuildPermissionSummary(role),
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            role.IsBulkAssignable);
    }

    private RoleDetails ToRoleDetails(RoleState role)
    {
        return new RoleDetails(
            role.RoleId,
            role.Name,
            role.Type,
            role.Scope,
            role.Priority,
            role.IsProtected ? "Protected" : role.Status,
            role.IsProtected,
            role.IsBulkAssignable,
            role.PermissionIds.ToArray());
    }

    private RoleUserAssignmentPreviewItem ToRoleUserAssignmentPreviewItem(UserState user)
    {
        return new RoleUserAssignmentPreviewItem(
            user.UserId,
            user.DisplayName,
            user.Email,
            user.DepartmentName,
            ToUserListItem(user).HighestPriorityRoleName,
            user.AccountStatus);
    }

    private AdminNotificationEventListItem ToNotificationEventListItem(NotificationEventState item)
    {
        var template = _notificationTemplates.FirstOrDefault(template => template.EventCode == item.EventCode);
        return new AdminNotificationEventListItem(
            item.EventId,
            item.EventCode,
            item.Name,
            item.Recipient,
            template?.Name ?? "Unlinked template",
            item.Status,
            item.UpdatedAtUtc);
    }

    private NotificationTemplateSummary ToNotificationTemplateSummary(NotificationTemplateState template)
    {
        return new NotificationTemplateSummary(
            template.TemplateId,
            template.EventCode,
            template.Name,
            template.Recipient,
            template.Subject,
            template.Body,
            template.Variables.ToArray(),
            template.Status,
            template.UpdatedAtUtc,
            template.UpdatedByUserId);
    }

    private static AdminNotificationOutboxItem ToNotificationOutboxItem(OutboxState item)
    {
        return new AdminNotificationOutboxItem(
            item.OutboxId,
            item.EventCode,
            item.EventCode,
            "Application-composed email",
            "Talent Pilot workflow",
            null,
            null,
            "Email",
            item.Status,
            item.AttemptCount,
            item.CreatedAtUtc,
            item.CreatedAtUtc,
            item.ProcessedAtUtc ?? item.CreatedAtUtc,
            item.ProcessedAtUtc,
            item.LastError,
            item.EventCode,
            string.Empty,
            null,
            null);
    }

    private UserState? FindUser(Guid tenantId, Guid userId)
    {
        return _users.FirstOrDefault(user => user.TenantId == tenantId && user.UserId == userId);
    }

    private RoleState FindRoleByCode(string code)
    {
        return _roles.First(role => role.Code == code);
    }

    private string FindRoleName(Guid roleId)
    {
        return _roles.FirstOrDefault(role => role.RoleId == roleId)?.Name ?? "Unknown role";
    }

    private string FindRoleName(Guid? roleId)
    {
        return roleId.HasValue ? FindRoleName(roleId.Value) : "Unassigned";
    }

    private string FindUserName(Guid? userId)
    {
        return userId.HasValue
            ? _users.FirstOrDefault(user => user.UserId == userId.Value)?.DisplayName ?? "Unknown user"
            : "Unassigned";
    }

    private string FindDepartmentName(Guid? departmentId)
    {
        return departmentId.HasValue
            ? _departments.FirstOrDefault(department => department.DepartmentId == departmentId.Value)?.Name ?? "All departments"
            : "All departments";
    }

    private string BuildPermissionSummary(RoleState role)
    {
        if (role.PermissionIds.Count == 0)
        {
            return "No permissions";
        }

        return string.Join(", ", role.PermissionIds.Take(3).Select(permissionId =>
            _permissions.FirstOrDefault(permission => permission.PermissionId == permissionId)?.DisplayName ?? permissionId));
    }

    private static string BuildInitials(string displayName)
    {
        var parts = displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        return string.Concat(parts);
    }

    private static Guid DepartmentIdFromName(string name)
    {
        var bytes = new byte[16];
        var hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(name);
        BitConverter.GetBytes(hashCode).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static string CodeFromName(string name)
    {
        var code = new string(name
            .Where(char.IsLetterOrDigit)
            .Take(12)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return code.PadRight(2, 'X');
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class TenantState
    {
        public Guid TenantId { get; init; }
        public string DisplayName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string AdminContactEmail { get; set; } = string.Empty;
        public string DefaultTimezone { get; set; } = string.Empty;
        public string DefaultCurrency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CareerDisplayName { get; set; } = string.Empty;
        public string? CompanyAddress { get; set; }
        public string? CompanyCity { get; set; }
        public string? CompanyCountry { get; set; }
        public string? OfficialEmail { get; set; }
        public string? OfficialPhone { get; set; }
        public string PrimaryColor { get; set; } = string.Empty;
        public bool CandidateLoginRequired { get; set; }
        public string CandidateCvFormat { get; set; } = string.Empty;
        public bool PublicJobsEnabled { get; set; }
        public int InviteExpiryDays { get; set; }
        public int ReapplyCooldownDays { get; set; }
        public string NotificationEmailProvider { get; set; } = NotificationEmailProviders.Resend;
        public string? LogoFileName { get; set; }
        public string? LogoContentType { get; set; }
        public string? LogoContentBase64 { get; set; }
        public bool SetupComplete { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class RoleState
    {
        public Guid RoleId { get; init; }
        public Guid TenantId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsProtected { get; init; }
        public bool IsBulkAssignable { get; init; }
        public List<string> PermissionIds { get; set; } = [];
    }

    private sealed class UserState
    {
        public Guid UserId { get; init; }
        public Guid TenantId { get; init; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public string? PasswordHash { get; init; }
        public List<Guid> RoleIds { get; set; } = [];
        public List<Guid> GroupIds { get; set; } = [];
        public string DepartmentName { get; init; } = string.Empty;
        public decimal? ExperienceYears { get; init; }
        public DateOnly? JoiningDate { get; init; }
        public int CompletedInterviewCount { get; init; }
        public DateTimeOffset? LastActiveAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class GroupState
    {
        public Guid GroupId { get; init; }
        public Guid TenantId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    private sealed class DepartmentState
    {
        public Guid DepartmentId { get; init; }
        public Guid TenantId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public Guid? LeadUserId { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }

    private sealed class SkillState
    {
        public Guid SkillId { get; init; }
        public Guid TenantId { get; init; }
        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = [];
        public bool IsVectorRelevant { get; init; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class CandidateSourceLabelState
    {
        public Guid CandidateSourceLabelId { get; init; }
        public Guid TenantId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string ReportingCategory { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; init; }
    }

    private sealed class InterviewTemplateState
    {
        public Guid InterviewTemplateId { get; init; }
        public Guid TenantId { get; init; }
        public Guid? DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class InterviewTemplateRoundState
    {
        public Guid InterviewTemplateRoundId { get; init; }
        public Guid TenantId { get; init; }
        public Guid InterviewTemplateId { get; init; }
        public int RoundOrder { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? OwnerRoleId { get; set; }
        public Guid? OwnerUserId { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsRequired { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class IntakeRoutingRuleState
    {
        public Guid JobRequestIntakeRoutingRuleId { get; init; }
        public Guid TenantId { get; init; }
        public Guid DepartmentId { get; init; }
        public string AssignmentType { get; set; } = string.Empty;
        public Guid? TargetUserId { get; set; }
        public Guid? TargetGroupId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed record WorkflowActionDefinition(
        Guid WorkflowTransitionId,
        string ActionKey,
        string ActionName);

    private sealed class NotificationEventState
    {
        public NotificationEventState(
            Guid eventId,
            Guid tenantId,
            string eventCode,
            string name,
            string recipient,
            string status,
            DateTimeOffset updatedAtUtc)
        {
            EventId = eventId;
            TenantId = tenantId;
            EventCode = eventCode;
            Name = name;
            Recipient = recipient;
            Status = status;
            UpdatedAtUtc = updatedAtUtc;
        }

        public Guid EventId { get; }
        public Guid TenantId { get; }
        public string EventCode { get; }
        public string Name { get; }
        public string Recipient { get; }
        public string Status { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    private sealed class NotificationTemplateState
    {
        public Guid TemplateId { get; init; }
        public Guid TenantId { get; init; }
        public string EventCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Recipient { get; init; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<string> Variables { get; init; } = [];
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public Guid UpdatedByUserId { get; set; }
    }

    private sealed class NotificationRecipientState
    {
        public Guid NotificationRecipientId { get; init; }
        public Guid TenantId { get; init; }
        public Guid NotificationEventId { get; init; }
        public Guid RecipientUserId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string? EntityType { get; init; }
        public Guid? EntityId { get; init; }
        public string MetadataJson { get; init; } = "{}";
        public DateTimeOffset? ReadAtUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
    }

    private sealed class TenantAiSettingsState
    {
        public Guid TenantId { get; init; }
        public string Provider { get; init; } = string.Empty;
        public string LlmModel { get; init; } = string.Empty;
        public string EmbeddingModel { get; init; } = string.Empty;
        public int EmbeddingDimensions { get; init; }
        public string VectorStore { get; init; } = string.Empty;
        public bool ModelSwitchingLocked { get; init; }
        public bool HumanReviewRequired { get; init; }
        public bool AutoRejectEnabled { get; init; }
    }

    private sealed class AiAgentDefinitionState
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Responsibility { get; init; } = string.Empty;
        public string InputSummary { get; init; } = string.Empty;
        public string OutputSummary { get; init; } = string.Empty;
        public string MvpBoundary { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }

    private sealed class OutboxState
    {
        public OutboxState(
            Guid outboxId,
            Guid tenantId,
            string eventCode,
            string status,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? processedAtUtc)
        {
            OutboxId = outboxId;
            TenantId = tenantId;
            EventCode = eventCode;
            Status = status;
            CreatedAtUtc = createdAtUtc;
            ProcessedAtUtc = processedAtUtc;
        }

        public Guid OutboxId { get; }
        public Guid TenantId { get; }
        public string EventCode { get; }
        public string Status { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset CreatedAtUtc { get; }
        public DateTimeOffset? ProcessedAtUtc { get; set; }
    }

    private sealed class AuditLogState
    {
        public Guid AuditLogId { get; init; }
        public Guid TenantId { get; init; }
        public DateTimeOffset OccurredAtUtc { get; init; }
        public Guid? ActorUserId { get; init; }
        public string ActorDisplayName { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public Guid? EntityId { get; init; }
        public string RecordLabel { get; init; } = string.Empty;
        public string EventSummary { get; init; } = string.Empty;
        public string Area { get; init; } = string.Empty;
        public string MetadataJson { get; init; } = "{}";
    }
}
