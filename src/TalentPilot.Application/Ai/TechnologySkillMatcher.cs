using System.Text.RegularExpressions;

namespace TalentPilot.Application.Ai;

public enum SkillMatchLevel
{
    Exact,
    StrongAdjacent,
    Transferable,
    BroadCategory,
    WeakOrMissing
}

public sealed record SkillMatchEvidence(
    string RequiredSkill,
    string? CandidateSkill,
    SkillMatchLevel MatchLevel,
    decimal Score,
    string Explanation,
    bool RampUpRequired,
    string? RampUpNotes,
    string Confidence);

public sealed record SkillMatchAssessment(IReadOnlyList<SkillMatchEvidence> Items)
{
    public decimal OverallScore => Items.Count == 0 ? 0m : Items.Average(item => item.Score);

    public string Confidence => OverallScore switch
    {
        >= 0.80m when WeakOrMissingMatches.Count == 0 => "high",
        >= 0.55m => "medium",
        _ => "low"
    };

    public IReadOnlyList<SkillMatchEvidence> ExactMatches => Items
        .Where(item => item.MatchLevel == SkillMatchLevel.Exact)
        .ToArray();

    public IReadOnlyList<SkillMatchEvidence> StrongAdjacentMatches => Items
        .Where(item => item.MatchLevel == SkillMatchLevel.StrongAdjacent)
        .ToArray();

    public IReadOnlyList<SkillMatchEvidence> TransferableMatches => Items
        .Where(item => item.MatchLevel == SkillMatchLevel.Transferable)
        .ToArray();

    public IReadOnlyList<SkillMatchEvidence> BroadCategoryMatches => Items
        .Where(item => item.MatchLevel == SkillMatchLevel.BroadCategory)
        .ToArray();

    public IReadOnlyList<SkillMatchEvidence> WeakOrMissingMatches => Items
        .Where(item => item.MatchLevel == SkillMatchLevel.WeakOrMissing)
        .ToArray();

    public string Summary => string.Join("; ", Items.Select(item =>
        $"{item.RequiredSkill}: {FormatLevel(item.MatchLevel)}" +
        (string.IsNullOrWhiteSpace(item.CandidateSkill) ? string.Empty : $" via {item.CandidateSkill}") +
        (string.IsNullOrWhiteSpace(item.RampUpNotes) ? string.Empty : $" ({item.RampUpNotes})")));

    public string HumanReviewNotes
    {
        get
        {
            var weak = JoinRequiredSkills(WeakOrMissingMatches);
            var rampUp = JoinRequiredSkills(Items.Where(item => item.RampUpRequired).ToArray());
            return string.Join(" ", new[]
            {
                weak.Length == 0 ? null : $"Human review should validate missing or weak required skills: {weak}.",
                rampUp.Length == 0 ? null : $"Ramp-up areas: {rampUp}.",
                "AI assessment is advisory; a human reviewer must make the next-step decision."
            }.Where(part => part is not null))!;
        }
    }

    public static string FormatLevel(SkillMatchLevel level) => level switch
    {
        SkillMatchLevel.Exact => "exact",
        SkillMatchLevel.StrongAdjacent => "strong adjacent",
        SkillMatchLevel.Transferable => "transferable",
        SkillMatchLevel.BroadCategory => "broad category",
        _ => "weak or missing"
    };

    private static string JoinRequiredSkills(IReadOnlyList<SkillMatchEvidence> items) =>
        string.Join(", ", items.Select(item => item.RequiredSkill));
}

public static class TechnologySkillMatcher
{
    private const decimal ExactScore = 1.00m;
    private const decimal StrongAdjacentScore = 0.82m;
    private const decimal TransferableScore = 0.62m;
    private const decimal BroadCategoryScore = 0.42m;
    private const decimal MissingScore = 0.00m;

    private static readonly HashSet<string> NoCategories = new(StringComparer.Ordinal);

    private static readonly HashSet<string> BackendCategories = NormalizeSet(
        "backend", "backend engineer", "backend developer", "api development", "server side",
        "microservices", "enterprise backend", "software engineer", "developer", "engineer");

    private static readonly HashSet<string> FrontendCategories = NormalizeSet(
        "frontend", "front end", "frontend engineer", "frontend developer", "web developer",
        "web development", "ui developer", "spa");

    private static readonly HashSet<string> DesktopCategories = NormalizeSet(
        "desktop development", "desktop developer", "windows desktop", "software engineer", "developer", "engineer");

    private static readonly HashSet<string> CloudCategories = NormalizeSet(
        "cloud engineer", "cloud infrastructure", "devops", "devops engineer", "site reliability", "infrastructure");

    private static readonly HashSet<string> EngineeringConceptCategories = NormalizeSet(
        "software architecture", "architecture patterns", "object oriented programming", "oop", "solid");

    private static readonly HashSet<string> SalesCategories = NormalizeSet(
        "sales", "sales executive", "business development", "bd", "account executive",
        "sales specialist", "customer handling", "client handling", "lead follow up");

    private static readonly HashSet<string> PresalesCategories = NormalizeSet(
        "presales", "pre sales", "sales engineering", "solution consulting", "proposal writing",
        "bid management", "business analysis", "solution design", "technical lead");

    private static readonly HashSet<string> HrCategories = NormalizeSet(
        "hr", "human resources", "hr executive", "hr operations", "hr coordinator",
        "talent acquisition", "recruitment", "recruiter", "onboarding", "employee lifecycle");

    private static readonly HashSet<string> FinanceCategories = NormalizeSet(
        "finance", "finance officer", "accounting", "accountant", "bookkeeping",
        "accounts payable", "accounts receivable", "audit", "financial reporting");

    private static readonly HashSet<string> MarketingCategories = NormalizeSet(
        "marketing", "marketing executive", "marketing specialist", "social media",
        "content marketing", "brand marketing", "campaign management", "community engagement");

    private static readonly HashSet<string> CustomerSuccessCategories = NormalizeSet(
        "customer success", "customer support", "technical support", "account management",
        "account manager", "client relationship", "relationship management", "helpdesk");

    private static readonly HashSet<string> ProjectManagementCategories = NormalizeSet(
        "project management", "project manager", "project coordination", "project coordinator",
        "delivery management", "program management", "operations project management");

    private static readonly HashSet<string> ProductManagementCategories = NormalizeSet(
        "product management", "product manager", "product owner", "business analyst",
        "requirements gathering", "backlog", "user stories");

    private static readonly HashSet<string> QaCategories = NormalizeSet(
        "qa", "quality assurance", "qa engineer", "manual qa", "manual testing",
        "test cases", "regression testing", "uat");

    private static readonly HashSet<string> DataCategories = NormalizeSet(
        "data", "analytics", "data analyst", "business analyst", "bi", "reporting analyst",
        "excel reporting", "dashboard", "sql reporting");

    private static readonly HashSet<string> OperationsCategories = NormalizeSet(
        "operations", "admin", "administration", "coordinator", "officer", "specialist", "manager");

    private static readonly HashSet<string> BackendRequirementKeys = NormalizeSet(
        "python", "fastapi", "django", "flask", "java", "spring boot", "hibernate", "jpa",
        "csharp", "dotnet", "dotnet core", "aspnet", "aspnet core", "nodejs", "expressjs", "nestjs",
        "rest api", "web api", "graphql", "postgresql", "sql server", "mysql");

    private static readonly HashSet<string> FrontendRequirementKeys = NormalizeSet(
        "react", "nextjs", "angular", "vue", "nuxt", "typescript", "rxjs", "ngrx", "redux", "zustand");

    private static readonly HashSet<string> FrontendBroadRequirementKeys = NormalizeSet(
        "react", "nextjs", "angular", "vue", "nuxt");

    private static readonly HashSet<string> DesktopRequirementKeys = NormalizeSet(
        "dotnet desktop", "wpf", "winforms", "windows forms", "desktop development");

    private static readonly HashSet<string> CloudRequirementKeys = NormalizeSet(
        "aws", "amazon web services", "azure", "gcp", "google cloud", "cloud infrastructure", "devops");

    private static readonly HashSet<string> EngineeringConceptRequirementKeys = NormalizeSet(
        "design patterns", "software design patterns", "architecture patterns", "oop", "object oriented programming", "solid");

    private static readonly HashSet<string> SalesRequirementKeys = NormalizeSet(
        "b2b saas sales", "saas sales", "b2b sales", "enterprise sales", "smb sales",
        "inside sales", "field sales", "account executive", "business development representative",
        "sales development representative", "account management", "channel sales", "partnerships",
        "lead generation", "proposal management", "contract negotiation", "crm pipeline management",
        "quota ownership", "product demos", "enterprise clients", "sales communication",
        "it services sales", "software outsourcing sales");

    private static readonly HashSet<string> PresalesRequirementKeys = NormalizeSet(
        "technical presales", "presales", "solution consulting", "proposal writing", "rfp",
        "rfi", "discovery calls", "solution estimation", "client requirement analysis",
        "demo preparation", "sow support", "pricing support", "bid management", "sales engineering");

    private static readonly HashSet<string> HrRequirementKeys = NormalizeSet(
        "hr operations", "hr business partner", "hrbp", "talent acquisition", "technical recruitment",
        "non technical recruitment", "software engineering hiring", "engineering role sourcing",
        "executive hiring", "volume hiring", "campus hiring", "contract hiring", "international hiring",
        "sourcing", "linkedin sourcing", "linkedin recruiter", "candidate screening", "screening candidates",
        "offer management", "candidate engagement", "ats usage", "hris administration", "payroll",
        "compensation and benefits", "employee relations", "performance management", "learning and development",
        "onboarding", "offboarding", "policy coordination", "policy development", "employer branding");

    private static readonly HashSet<string> FinanceRequirementKeys = NormalizeSet(
        "accounting", "bookkeeping", "accounts payable", "accounts receivable", "payroll finance",
        "financial reporting", "fpa", "fp&a", "budgeting", "forecasting", "variance analysis",
        "taxation", "audit", "treasury", "corporate finance", "revenue recognition", "cost accounting",
        "financial modeling", "management reporting", "compliance reporting");

    private static readonly HashSet<string> MarketingRequirementKeys = NormalizeSet(
        "digital marketing", "performance marketing", "paid ads", "google ads", "meta ads",
        "linkedin ads", "roas tracking", "campaign optimization", "analytics", "marketing analytics",
        "seo", "technical seo", "content marketing", "product marketing", "brand marketing",
        "social media marketing", "email marketing", "marketing operations", "growth marketing",
        "event marketing", "community marketing", "copywriting", "campaign management");

    private static readonly HashSet<string> CustomerSuccessRequirementKeys = NormalizeSet(
        "customer success", "customer success manager", "customer support", "technical support",
        "account management", "onboarding", "renewal management", "churn reduction",
        "customer training", "implementation support", "helpdesk operations", "sla management",
        "escalation handling", "product adoption", "account health tracking");

    private static readonly HashSet<string> QaRequirementKeys = NormalizeSet(
        "manual qa", "manual testing", "automation qa", "automation testing", "test automation",
        "selenium", "playwright", "cypress", "api testing", "performance testing", "security testing",
        "mobile testing", "web testing", "test planning", "regression testing", "regression automation",
        "uat coordination", "test case design", "ci test integration");

    private static readonly HashSet<string> ProjectManagementRequirementKeys = NormalizeSet(
        "software project management", "agile project management", "scrum master", "delivery management",
        "program management", "client facing project management", "internal operations project management",
        "resource planning", "budget tracking", "risk management", "sprint planning",
        "stakeholder management", "stakeholder communication", "client communication", "vendor management",
        "delivery ownership", "agile scrum");

    private static readonly HashSet<string> ProductManagementRequirementKeys = NormalizeSet(
        "product owner", "product manager", "technical product manager", "growth product manager",
        "product strategy", "roadmap planning", "backlog management", "user stories",
        "market research", "customer discovery", "prioritization", "product analytics",
        "go to market support", "stakeholder alignment", "acceptance criteria", "requirements gathering");

    private static readonly HashSet<string> DataRequirementKeys = NormalizeSet(
        "data analyst", "business analyst", "bi developer", "bi development", "data engineer",
        "analytics engineer", "reporting analyst", "data visualization", "sql analytics",
        "etl", "elt", "data warehousing", "dashboard development", "statistical analysis",
        "product analytics", "financial analytics", "power bi", "tableau", "looker", "dbt",
        "airflow", "snowflake", "bigquery", "redshift");

    private static readonly Dictionary<string, string[]> StrongAdjacentMap = new(StringComparer.Ordinal)
    {
        ["dotnet"] = CanonicalArray("csharp", "aspnet", "aspnet core", "aspnet mvc", "web api", "entity framework", "blazor"),
        ["dotnet core"] = CanonicalArray("dotnet", "csharp", "aspnet core", "aspnet mvc", "web api", "entity framework"),
        ["aspnet core"] = CanonicalArray("dotnet", "dotnet core", "csharp", "aspnet", "aspnet mvc", "web api"),
        ["java"] = CanonicalArray("spring", "spring boot", "java ee", "hibernate", "jpa", "maven", "gradle"),
        ["spring boot"] = CanonicalArray("java", "spring", "spring mvc", "java ee", "hibernate", "jpa"),
        ["hibernate"] = CanonicalArray("jpa", "java", "spring boot"),
        ["python"] = CanonicalArray("django", "flask", "fastapi", "pytest", "sqlalchemy", "celery"),
        ["fastapi"] = CanonicalArray("python", "django", "flask", "sqlalchemy"),
        ["django"] = CanonicalArray("python", "flask", "fastapi"),
        ["nodejs"] = CanonicalArray("expressjs", "nestjs", "graphql"),
        ["expressjs"] = CanonicalArray("nodejs", "nestjs", "javascript", "typescript"),
        ["nestjs"] = CanonicalArray("nodejs", "expressjs", "typescript"),
        ["react"] = CanonicalArray("nextjs", "redux", "zustand", "react query", "typescript"),
        ["nextjs"] = CanonicalArray("react", "typescript"),
        ["angular"] = CanonicalArray("rxjs", "angular material"),
        ["vue"] = CanonicalArray("nuxt", "pinia", "vuex", "typescript"),
        ["nuxt"] = CanonicalArray("vue", "typescript"),
        ["ngrx"] = CanonicalArray("angular", "rxjs"),
        ["redux"] = CanonicalArray("zustand", "react"),
        ["rest api"] = CanonicalArray("web api", "api design", "graphql"),
        ["dotnet desktop"] = CanonicalArray("wpf", "winforms", "windows forms"),
        ["wpf"] = CanonicalArray("csharp", "dotnet", "dotnet desktop", "winforms"),
        ["winforms"] = CanonicalArray("csharp", "dotnet", "dotnet desktop", "wpf"),
        ["aws"] = CanonicalArray("amazon web services", "lambda", "ecs", "ec2", "s3", "cloudformation"),
        ["azure"] = CanonicalArray("azure functions", "azure devops", "app service"),
        ["kubernetes"] = CanonicalArray("aks", "eks", "gke", "helm"),
        ["terraform"] = CanonicalArray("infrastructure as code", "iac"),
        ["ci cd"] = CanonicalArray("github actions", "gitlab ci", "jenkins", "azure pipelines", "azure devops"),
        ["monitoring"] = CanonicalArray("prometheus", "grafana", "prometheus grafana", "observability"),
        ["design patterns"] = CanonicalArray("software design patterns", "architecture patterns"),
        ["b2b saas sales"] = CanonicalArray("saas sales", "b2b technology sales", "software sales"),
        ["saas sales"] = CanonicalArray("b2b saas sales", "b2b technology sales", "software sales"),
        ["crm pipeline management"] = CanonicalArray("salesforce", "hubspot", "zoho crm", "pipedrive", "crm pipeline", "pipeline management"),
        ["quota ownership"] = CanonicalArray("quota carrying", "sales quota", "revenue target", "sales target"),
        ["product demos"] = CanonicalArray("product demonstration", "demo preparation", "solution demos"),
        ["enterprise clients"] = CanonicalArray("enterprise sales", "enterprise accounts", "large accounts"),
        ["sales communication"] = CanonicalArray("client communication", "customer communication"),
        ["technical presales"] = CanonicalArray("solution consulting", "sales engineering", "technical solutioning"),
        ["solution consulting"] = CanonicalArray("technical presales", "sales engineering", "solution design"),
        ["rfp"] = CanonicalArray("rfi", "proposal writing", "bid management"),
        ["client requirement analysis"] = CanonicalArray("requirements gathering", "business analysis", "discovery calls"),
        ["technical recruitment"] = CanonicalArray("it recruitment", "engineering recruitment", "tech recruiter", "technical recruiter", "software engineering hiring"),
        ["software engineering hiring"] = CanonicalArray("technical recruitment", "it recruitment", "engineering recruitment"),
        ["engineering role sourcing"] = CanonicalArray("technical sourcing", "linkedin recruiter", "linkedin sourcing"),
        ["ats usage"] = CanonicalArray("ats", "applicant tracking system", "greenhouse", "lever"),
        ["candidate screening"] = CanonicalArray("screening candidates", "screening"),
        ["linkedin sourcing"] = CanonicalArray("linkedin recruiter", "sourcing"),
        ["hr operations"] = CanonicalArray("employee lifecycle", "hris administration", "onboarding", "offboarding"),
        ["management reporting"] = CanonicalArray("financial reporting", "monthly closing"),
        ["financial reporting"] = CanonicalArray("management reporting", "monthly closing"),
        ["performance marketing"] = CanonicalArray("paid ads", "google ads", "meta ads", "campaign optimization", "roas tracking"),
        ["paid ads"] = CanonicalArray("google ads", "meta ads", "linkedin ads"),
        ["analytics"] = CanonicalArray("google analytics", "ga4", "marketing analytics"),
        ["customer success"] = CanonicalArray("customer success manager", "customer onboarding", "customer engagement"),
        ["product adoption"] = CanonicalArray("customer onboarding", "implementation support", "customer training"),
        ["account health tracking"] = CanonicalArray("customer health", "health score", "gainsight"),
        ["renewal management"] = CanonicalArray("renewals", "contract renewal", "retention"),
        ["automation testing"] = CanonicalArray("test automation", "selenium", "playwright", "cypress", "appium"),
        ["api testing"] = CanonicalArray("postman", "restassured", "rest assured", "api test"),
        ["regression automation"] = CanonicalArray("automation testing", "test automation"),
        ["ci test integration"] = CanonicalArray("ci cd", "github actions", "jenkins", "gitlab ci"),
        ["software project management"] = CanonicalArray("software delivery", "agile project management", "delivery management"),
        ["agile scrum"] = CanonicalArray("agile", "scrum", "kanban"),
        ["sprint planning"] = CanonicalArray("scrum", "agile ceremonies"),
        ["client communication"] = CanonicalArray("stakeholder communication", "client management", "customer communication"),
        ["stakeholder communication"] = CanonicalArray("client communication", "stakeholder management"),
        ["product owner"] = CanonicalArray("backlog management", "user stories", "acceptance criteria"),
        ["user stories"] = CanonicalArray("requirements gathering", "acceptance criteria"),
        ["acceptance criteria"] = CanonicalArray("user stories", "requirements documentation", "uat support"),
        ["sql analytics"] = CanonicalArray("sql", "sql querying"),
        ["bi development"] = CanonicalArray("power bi", "tableau", "looker", "dashboard development"),
        ["dashboard development"] = CanonicalArray("power bi", "tableau", "looker", "data visualization"),
        ["data warehousing"] = CanonicalArray("snowflake", "bigquery", "redshift"),
        ["etl"] = CanonicalArray("elt", "airflow", "dbt", "azure data factory", "databricks")
    };

    private static readonly Dictionary<string, string[]> TransferableMap = new(StringComparer.Ordinal)
    {
        ["python"] = CanonicalArray("java", "spring boot", "dotnet", "dotnet core", "csharp", "nodejs"),
        ["java"] = CanonicalArray("dotnet", "dotnet core", "csharp", "aspnet core", "enterprise backend", "rest api"),
        ["spring boot"] = CanonicalArray("dotnet", "dotnet core", "aspnet core", "csharp", "enterprise backend", "rest api"),
        ["hibernate"] = CanonicalArray("entity framework", "ef core", "dotnet core", "aspnet core"),
        ["dotnet"] = CanonicalArray("java", "spring boot", "enterprise backend", "rest api"),
        ["dotnet core"] = CanonicalArray("java", "spring boot", "enterprise backend", "rest api"),
        ["react"] = CanonicalArray("angular", "vue", "spa"),
        ["angular"] = CanonicalArray("react", "vue", "spa"),
        ["vue"] = CanonicalArray("react", "angular", "spa"),
        ["ngrx"] = CanonicalArray("redux", "zustand"),
        ["redux"] = CanonicalArray("ngrx", "vuex", "pinia"),
        ["nodejs"] = CanonicalArray("python", "java", "dotnet", "rest api", "serverless"),
        ["postgresql"] = CanonicalArray("sql", "sql server", "mysql", "database design", "relational database", "oracle", "sqlite"),
        ["sql server"] = CanonicalArray("sql", "postgresql", "mysql", "database design", "relational database", "oracle", "sqlite"),
        ["dotnet desktop"] = CanonicalArray("csharp", "dotnet", "dotnet core", "aspnet", "aspnet mvc", "aspnet core", "web development"),
        ["wpf"] = CanonicalArray("aspnet", "aspnet mvc", "aspnet core", "web development"),
        ["winforms"] = CanonicalArray("aspnet", "aspnet mvc", "aspnet core", "web development"),
        ["aws"] = CanonicalArray("azure", "gcp", "google cloud", "cloud infrastructure", "devops"),
        ["azure"] = CanonicalArray("aws", "gcp", "google cloud", "cloud infrastructure", "devops"),
        ["b2b saas sales"] = CanonicalArray("it services sales", "software outsourcing sales", "b2b sales"),
        ["enterprise sales"] = CanonicalArray("smb sales", "inside sales", "field sales"),
        ["sales communication"] = CanonicalArray("retail sales", "customer handling", "lead follow up", "client handling"),
        ["technical presales"] = CanonicalArray("business analysis", "technical lead", "solution design", "sales"),
        ["solution estimation"] = CanonicalArray("software estimation", "technical lead", "business analysis"),
        ["technical recruitment"] = CanonicalArray("non technical recruitment", "sales recruitment", "general recruitment", "recruitment"),
        ["engineering role sourcing"] = CanonicalArray("sales recruitment", "job posting", "candidate screening", "sourcing"),
        ["candidate screening"] = CanonicalArray("interview coordination", "job posting"),
        ["hr business partner"] = CanonicalArray("hr operations", "employee relations", "performance management"),
        ["payroll"] = CanonicalArray("hr operations", "compensation and benefits", "payroll finance"),
        ["compensation and benefits"] = CanonicalArray("payroll", "hr operations"),
        ["fpa"] = CanonicalArray("accounting", "financial reporting", "audit", "business analysis"),
        ["financial modeling"] = CanonicalArray("advanced excel"),
        ["taxation"] = CanonicalArray("accounting", "audit"),
        ["customer success"] = CanonicalArray("account management", "customer support", "technical support", "relationship management"),
        ["renewal management"] = CanonicalArray("account management", "upselling", "retention"),
        ["product adoption"] = CanonicalArray("client training"),
        ["performance marketing"] = CanonicalArray("social media marketing", "content marketing", "campaign management"),
        ["roas tracking"] = CanonicalArray("marketing analytics", "campaign reporting"),
        ["automation testing"] = CanonicalArray("manual testing", "manual qa", "test cases", "regression testing"),
        ["regression automation"] = CanonicalArray("regression testing", "manual testing"),
        ["software project management"] = CanonicalArray("project coordination", "project management", "internal reporting"),
        ["backlog management"] = CanonicalArray("requirements gathering", "user stories", "documentation"),
        ["stakeholder communication"] = CanonicalArray("client communication", "requirements gathering"),
        ["data warehousing"] = CanonicalArray("sql analytics", "sql reporting", "excel reporting"),
        ["etl"] = CanonicalArray("sql analytics", "data reporting"),
        ["bi development"] = CanonicalArray("excel reporting", "sql reporting", "dashboard")
    };

    private static readonly Dictionary<string, string[]> ImpliedEvidenceKeyMap = new(StringComparer.Ordinal)
    {
        ["github actions"] = CanonicalArray("ci cd"),
        ["gitlab ci"] = CanonicalArray("ci cd"),
        ["jenkins"] = CanonicalArray("ci cd"),
        ["azure pipelines"] = CanonicalArray("ci cd"),
        ["aks"] = CanonicalArray("kubernetes"),
        ["eks"] = CanonicalArray("kubernetes"),
        ["gke"] = CanonicalArray("kubernetes"),
        ["prometheus"] = CanonicalArray("monitoring"),
        ["grafana"] = CanonicalArray("monitoring"),
        ["prometheus grafana"] = CanonicalArray("monitoring"),
        ["salesforce"] = CanonicalArray("crm pipeline management"),
        ["hubspot"] = CanonicalArray("crm pipeline management"),
        ["zoho crm"] = CanonicalArray("crm pipeline management"),
        ["pipedrive"] = CanonicalArray("crm pipeline management"),
        ["greenhouse"] = CanonicalArray("ats usage"),
        ["lever"] = CanonicalArray("ats usage"),
        ["applicant tracking system"] = CanonicalArray("ats usage"),
        ["google ads"] = CanonicalArray("paid ads"),
        ["meta ads"] = CanonicalArray("paid ads"),
        ["linkedin ads"] = CanonicalArray("paid ads"),
        ["power bi"] = CanonicalArray("bi development", "dashboard development"),
        ["tableau"] = CanonicalArray("bi development", "dashboard development"),
        ["looker"] = CanonicalArray("bi development", "dashboard development")
    };

    public static SkillMatchAssessment Assess(
        IReadOnlyList<string> requiredSkills,
        IReadOnlyList<string> evidencedSkills,
        params string?[] evidenceText)
    {
        var evidence = BuildEvidence(evidencedSkills, evidenceText);
        var items = requiredSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(requiredSkill => Classify(requiredSkill, evidence))
            .ToArray();

        return new SkillMatchAssessment(items);
    }

    public static bool IsExactMatch(SkillMatchEvidence evidence) =>
        evidence.MatchLevel == SkillMatchLevel.Exact;

    public static string FormatAssessmentForPrompt(SkillMatchAssessment assessment)
    {
        if (assessment.Items.Count == 0)
        {
            return "No required skills were provided.";
        }

        return string.Join(Environment.NewLine, assessment.Items.Select(item =>
            $"- {item.RequiredSkill}: {SkillMatchAssessment.FormatLevel(item.MatchLevel)}" +
            (string.IsNullOrWhiteSpace(item.CandidateSkill) ? string.Empty : $" from {item.CandidateSkill}") +
            $"; score {(item.Score * 100m):0.#}%; confidence {item.Confidence}; {item.Explanation}" +
            (string.IsNullOrWhiteSpace(item.RampUpNotes) ? string.Empty : $" Ramp-up: {item.RampUpNotes}")));
    }

    public static string BuildSkillSummary(SkillMatchAssessment assessment)
    {
        var exact = JoinRequiredSkills(assessment.ExactMatches);
        var adjacent = JoinRequiredSkills(assessment.StrongAdjacentMatches);
        var transferable = JoinRequiredSkills(assessment.TransferableMatches);
        var broad = JoinRequiredSkills(assessment.BroadCategoryMatches);
        var missing = JoinRequiredSkills(assessment.WeakOrMissingMatches);

        return string.Join(" ", new[]
        {
            exact.Length == 0 ? null : $"Exact: {exact}.",
            adjacent.Length == 0 ? null : $"Adjacent: {adjacent}.",
            transferable.Length == 0 ? null : $"Transferable: {transferable}.",
            broad.Length == 0 ? null : $"Broad only: {broad}.",
            missing.Length == 0 ? null : $"Missing/weak: {missing}."
        }.Where(part => part is not null))!;
    }

    public static IReadOnlyList<string> BuildStrengthNotes(SkillMatchAssessment assessment)
    {
        var notes = new List<string>();
        if (assessment.ExactMatches.Count > 0)
        {
            notes.Add($"Exact required skill evidence: {JoinRequiredSkills(assessment.ExactMatches)}.");
        }

        if (assessment.StrongAdjacentMatches.Count > 0)
        {
            notes.Add($"Strong adjacent skill evidence: {JoinRequiredSkills(assessment.StrongAdjacentMatches)}.");
        }

        if (assessment.TransferableMatches.Count > 0)
        {
            notes.Add($"Transferable skill or role-domain evidence: {JoinRequiredSkills(assessment.TransferableMatches)}; ramp-up is required before treating these as direct matches.");
        }

        return notes;
    }

    public static IReadOnlyList<string> BuildGapNotes(SkillMatchAssessment assessment)
    {
        var notes = new List<string>();
        notes.AddRange(assessment.BroadCategoryMatches.Select(item =>
            $"{item.RequiredSkill}: only broad category evidence is present; validate hands-on sub-domain, tool, process, or ownership depth."));
        notes.AddRange(assessment.WeakOrMissingMatches.Select(item =>
            $"{item.RequiredSkill}: no direct required-skill or role-domain evidence was found."));
        notes.AddRange(assessment.Items
            .Where(item => item.RampUpRequired && item.MatchLevel is SkillMatchLevel.StrongAdjacent or SkillMatchLevel.Transferable)
            .Select(item => $"{item.RequiredSkill}: {item.RampUpNotes}"));

        return notes;
    }

    public static string BuildDirectEvidenceWarning(SkillMatchAssessment assessment)
    {
        var item = assessment.Items.FirstOrDefault(item =>
            item.MatchLevel is SkillMatchLevel.Transferable or SkillMatchLevel.BroadCategory or SkillMatchLevel.WeakOrMissing);
        if (item is null)
        {
            return string.Empty;
        }

        return item.MatchLevel switch
        {
            SkillMatchLevel.Transferable =>
                $"{item.RequiredSkill} is required, but no direct {item.RequiredSkill} evidence is recorded; {item.CandidateSkill} is transferable only and needs ramp-up.",
            SkillMatchLevel.BroadCategory =>
                $"{item.RequiredSkill} is required, but the available evidence is only broad category experience and does not prove hands-on {item.RequiredSkill} depth.",
            _ =>
                $"{item.RequiredSkill} is required, but no direct {item.RequiredSkill} evidence is recorded."
        };
    }

    private static SkillMatchEvidence Classify(string requiredSkill, EvidenceIndex evidence)
    {
        var requiredKey = Canonicalize(requiredSkill);
        if (TryFind(evidence.SkillKeys, requiredKey, out var exactSkill))
        {
            return new SkillMatchEvidence(
                requiredSkill,
                exactSkill,
                SkillMatchLevel.Exact,
                ExactScore,
                $"{exactSkill} directly evidences {requiredSkill}.",
                false,
                null,
                "high");
        }

        if (TryFindRelated(StrongAdjacentMap, requiredKey, evidence.SkillKeys, out var adjacentSkill))
        {
            return new SkillMatchEvidence(
                requiredSkill,
                adjacentSkill,
                SkillMatchLevel.StrongAdjacent,
                StrongAdjacentScore,
                $"{adjacentSkill} is a close skill, tool, role, or process match for {requiredSkill}, but it is not the exact same requirement.",
                true,
                $"Validate hands-on {requiredSkill} usage, ownership, or domain depth before treating this as exact.",
                "medium");
        }

        if (TryFindRelated(TransferableMap, requiredKey, evidence.SkillKeys, out var transferableSkill))
        {
            return new SkillMatchEvidence(
                requiredSkill,
                transferableSkill,
                SkillMatchLevel.Transferable,
                TransferableScore,
                $"{transferableSkill} provides transferable concepts or workflow experience for {requiredSkill}, but the required sub-domain, toolset, ownership level, or platform differs.",
                true,
                $"Ramp-up required on {requiredSkill} tools, process expectations, domain context, and evidence depth.",
                "medium");
        }

        if (TryFindBroadCategory(requiredKey, evidence, out var broadEvidence))
        {
            return new SkillMatchEvidence(
                requiredSkill,
                broadEvidence,
                SkillMatchLevel.BroadCategory,
                BroadCategoryScore,
                $"{broadEvidence} only supports a broad department or role-family category for {requiredSkill}; no direct sub-domain evidence was found.",
                true,
                $"Require human validation before assuming {requiredSkill} capability.",
                "low");
        }

        return new SkillMatchEvidence(
            requiredSkill,
            null,
            SkillMatchLevel.WeakOrMissing,
            MissingScore,
            $"No reliable evidence for {requiredSkill} was found.",
            true,
            $"Missing direct {requiredSkill} evidence.",
            "low");
    }

    private static bool TryFindBroadCategory(string requiredKey, EvidenceIndex evidence, out string broadEvidence)
    {
        var categories = RequiredCategorySet(requiredKey);
        foreach (var candidate in evidence.SkillKeys)
        {
            if (categories.Contains(candidate.Key))
            {
                broadEvidence = candidate.Value;
                return true;
            }
        }

        foreach (var phrase in categories)
        {
            if (ContainsNonNegatedSkillPhrase(evidence.NormalizedText, phrase))
            {
                broadEvidence = phrase;
                return true;
            }
        }

        broadEvidence = string.Empty;
        return false;
    }

    private static IReadOnlySet<string> RequiredCategorySet(string requiredKey)
    {
        if (FrontendRequirementKeys.Contains(requiredKey))
        {
            return FrontendBroadRequirementKeys.Contains(requiredKey)
                ? FrontendCategories
                : NoCategories;
        }

        if (CloudRequirementKeys.Contains(requiredKey))
        {
            return CloudCategories;
        }

        if (EngineeringConceptRequirementKeys.Contains(requiredKey))
        {
            return EngineeringConceptCategories;
        }

        if (SalesRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificSalesRequirement(requiredKey) ? NoCategories : SalesCategories;
        }

        if (PresalesRequirementKeys.Contains(requiredKey))
        {
            return PresalesCategories;
        }

        if (HrRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificHrRequirement(requiredKey) ? NoCategories : HrCategories;
        }

        if (FinanceRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificFinanceRequirement(requiredKey) ? NoCategories : FinanceCategories;
        }

        if (MarketingRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificMarketingRequirement(requiredKey) ? NoCategories : MarketingCategories;
        }

        if (CustomerSuccessRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificCustomerSuccessRequirement(requiredKey) ? NoCategories : CustomerSuccessCategories;
        }

        if (QaRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificQaRequirement(requiredKey) ? NoCategories : QaCategories;
        }

        if (ProjectManagementRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificProjectRequirement(requiredKey) ? NoCategories : ProjectManagementCategories;
        }

        if (ProductManagementRequirementKeys.Contains(requiredKey))
        {
            return ProductManagementCategories;
        }

        if (DataRequirementKeys.Contains(requiredKey))
        {
            return IsSpecificDataRequirement(requiredKey) ? NoCategories : DataCategories;
        }

        if (DesktopRequirementKeys.Contains(requiredKey))
        {
            return DesktopCategories;
        }

        return BackendRequirementKeys.Contains(requiredKey)
            ? BackendCategories
            : OperationsCategories;
    }

    private static bool IsSpecificSalesRequirement(string requiredKey) =>
        requiredKey is "b2b saas sales" or "saas sales" or "enterprise sales" or "crm pipeline management"
            or "quota ownership" or "product demos" or "enterprise clients" or "contract negotiation";

    private static bool IsSpecificHrRequirement(string requiredKey) =>
        requiredKey is "candidate screening" or "linkedin sourcing" or "linkedin recruiter" or "ats usage" or "payroll"
            or "compensation and benefits" or "employee relations";

    private static bool IsSpecificFinanceRequirement(string requiredKey) =>
        requiredKey is "budgeting" or "forecasting" or "variance analysis" or "financial modeling"
            or "taxation" or "treasury" or "revenue recognition" or "cost accounting";

    private static bool IsSpecificMarketingRequirement(string requiredKey) =>
        requiredKey is "paid ads" or "google ads" or "meta ads" or "linkedin ads"
            or "roas tracking" or "campaign optimization" or "analytics" or "marketing analytics"
            or "technical seo";

    private static bool IsSpecificCustomerSuccessRequirement(string requiredKey) =>
        requiredKey is "product adoption" or "churn reduction" or "account health tracking";

    private static bool IsSpecificQaRequirement(string requiredKey) =>
        requiredKey is "selenium" or "playwright" or "cypress" or "api testing" or "performance testing"
            or "security testing" or "ci test integration";

    private static bool IsSpecificProjectRequirement(string requiredKey) =>
        requiredKey is "agile scrum" or "sprint planning" or "client communication" or "risk management" or "delivery ownership";

    private static bool IsSpecificDataRequirement(string requiredKey) =>
        requiredKey is "data warehousing" or "etl" or "elt" or "dbt" or "airflow" or "snowflake"
            or "bigquery" or "redshift";

    private static bool TryFindRelated(
        IReadOnlyDictionary<string, string[]> map,
        string requiredKey,
        IReadOnlyDictionary<string, string> candidateKeys,
        out string candidateSkill)
    {
        if (map.TryGetValue(requiredKey, out var related))
        {
            foreach (var key in related)
            {
                if (TryFind(candidateKeys, key, out candidateSkill))
                {
                    return true;
                }
            }
        }

        foreach (var candidate in candidateKeys)
        {
            if (map.TryGetValue(candidate.Key, out var reverse) && reverse.Contains(requiredKey, StringComparer.Ordinal))
            {
                candidateSkill = candidate.Value;
                return true;
            }
        }

        candidateSkill = string.Empty;
        return false;
    }

    private static bool TryFind(IReadOnlyDictionary<string, string> candidateKeys, string key, out string candidateSkill)
    {
        if (candidateKeys.TryGetValue(key, out candidateSkill!))
        {
            return true;
        }

        foreach (var candidate in candidateKeys)
        {
            if (ContainsSkillPhrase(candidate.Key, key))
            {
                candidateSkill = candidate.Value;
                return true;
            }
        }

        candidateSkill = string.Empty;
        return false;
    }

    private static EvidenceIndex BuildEvidence(IReadOnlyList<string> evidencedSkills, string?[] evidenceText)
    {
        var skills = evidencedSkills
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var skillKeys = skills
            .Select(skill => new KeyValuePair<string, string>(Canonicalize(skill), skill))
            .GroupBy(item => item.Key, item => item.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var text = string.Join(' ', evidenceText.Where(value => !string.IsNullOrWhiteSpace(value)));
        var textKeys = ExtractKnownKeys(text);
        foreach (var key in textKeys)
        {
            skillKeys.TryAdd(key, key);
        }

        AddImpliedEvidenceKeys(skillKeys);

        return new EvidenceIndex(skillKeys, CanonicalizeText(text));
    }

    private static void AddImpliedEvidenceKeys(Dictionary<string, string> skillKeys)
    {
        foreach (var sourceKey in skillKeys.Keys.ToArray())
        {
            if (!ImpliedEvidenceKeyMap.TryGetValue(sourceKey, out var impliedKeys))
            {
                continue;
            }

            foreach (var impliedKey in impliedKeys)
            {
                skillKeys.TryAdd(impliedKey, skillKeys[sourceKey]);
            }
        }
    }

    private static IEnumerable<string> ExtractKnownKeys(string text)
    {
        var normalized = CanonicalizeText(text);
        return StrongAdjacentMap.Keys
            .Concat(StrongAdjacentMap.Values.SelectMany(value => value))
            .Concat(TransferableMap.Keys)
            .Concat(TransferableMap.Values.SelectMany(value => value))
            .Concat(ImpliedEvidenceKeyMap.Keys)
            .Concat(ImpliedEvidenceKeyMap.Values.SelectMany(value => value))
            .Concat(BackendCategories)
            .Concat(FrontendCategories)
            .Concat(DesktopCategories)
            .Concat(CloudCategories)
            .Concat(EngineeringConceptCategories)
            .Concat(SalesCategories)
            .Concat(PresalesCategories)
            .Concat(HrCategories)
            .Concat(FinanceCategories)
            .Concat(MarketingCategories)
            .Concat(CustomerSuccessCategories)
            .Concat(ProjectManagementCategories)
            .Concat(ProductManagementCategories)
            .Concat(QaCategories)
            .Concat(DataCategories)
            .Concat(OperationsCategories)
            .Where(key => ContainsNonNegatedSkillPhrase(normalized, key))
            .Distinct(StringComparer.Ordinal);
    }

    private static string JoinRequiredSkills(IReadOnlyList<SkillMatchEvidence> items) =>
        string.Join(", ", items.Select(item => item.RequiredSkill));

    private static string Canonicalize(string value)
    {
        var normalized = CanonicalizeText(value);

        return normalized switch
        {
            "net" or ".net" or "dotnet framework" => "dotnet",
            "net core" or ".net core" => "dotnet core",
            "aspnet mvc" or "asp net mvc" => "aspnet mvc",
            "aspnet core" or "asp net core" => "aspnet core",
            "rest apis" or "restful api" or "restful apis" => "rest api",
            "web apis" => "web api",
            "postgres" => "postgresql",
            "sqlserver" => "sql server",
            "amazon web services" => "aws",
            "google cloud" or "google cloud platform" => "gcp",
            "software design patterns" => "design patterns",
            "windows forms" => "winforms",
            "fp a" => "fpa",
            "pre sales" => "presales",
            "tech recruiter" or "technical recruiter" or "it recruiter" or "engineering recruiter" => "technical recruitment",
            "screening candidates" => "candidate screening",
            "customer success manager" or "csm" => "customer success",
            "account manager" => "account management",
            "requirement gathering" => "requirements gathering",
            "requirement documentation" => "requirements documentation",
            "qa automation" or "automation qa" => "automation testing",
            "test automation" => "automation testing",
            "google analytics" or "ga4" => "analytics",
            "powerbi" => "power bi",
            "cicd" => "ci cd",
            "iac" => "infrastructure as code",
            _ => normalized
        };
    }

    private static string CanonicalizeText(string value)
    {
        return NormalizeText(value)
            .Replace("node js", "nodejs", StringComparison.Ordinal)
            .Replace("node.js", "nodejs", StringComparison.Ordinal)
            .Replace("next js", "nextjs", StringComparison.Ordinal)
            .Replace("next.js", "nextjs", StringComparison.Ordinal)
            .Replace("nuxt js", "nuxt", StringComparison.Ordinal)
            .Replace("nuxt.js", "nuxt", StringComparison.Ordinal)
            .Replace("express js", "expressjs", StringComparison.Ordinal)
            .Replace("express.js", "expressjs", StringComparison.Ordinal)
            .Replace("nest js", "nestjs", StringComparison.Ordinal)
            .Replace("nest.js", "nestjs", StringComparison.Ordinal)
            .Replace("asp net", "aspnet", StringComparison.Ordinal)
            .Replace("asp dotnet", "aspnet", StringComparison.Ordinal)
            .Replace("dot net", "dotnet", StringComparison.Ordinal)
            .Replace("c sharp", "csharp", StringComparison.Ordinal)
            .Replace("rest apis", "rest api", StringComparison.Ordinal)
            .Replace("restful apis", "rest api", StringComparison.Ordinal)
            .Replace("restful api", "rest api", StringComparison.Ordinal)
            .Replace("web apis", "web api", StringComparison.Ordinal)
            .Replace("software design patterns", "design patterns", StringComparison.Ordinal)
            .Replace("amazon web services", "aws", StringComparison.Ordinal)
            .Replace("google cloud platform", "gcp", StringComparison.Ordinal)
            .Replace("google cloud", "gcp", StringComparison.Ordinal)
            .Replace("fp a", "fpa", StringComparison.Ordinal)
            .Replace("pre sales", "presales", StringComparison.Ordinal)
            .Replace("powerbi", "power bi", StringComparison.Ordinal)
            .Replace("ci cd", "ci cd", StringComparison.Ordinal)
            .Replace("cicd", "ci cd", StringComparison.Ordinal)
            .Replace("qa automation", "automation testing", StringComparison.Ordinal)
            .Replace("automation qa", "automation testing", StringComparison.Ordinal);
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace(".net", " dotnet ", StringComparison.Ordinal)
            .Replace("c#", " csharp ", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal);

        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static bool ContainsSkillPhrase(string haystack, string phrase)
    {
        var haystackTokens = haystack
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var phraseTokens = phrase
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (phraseTokens.Length == 0 || haystackTokens.Length < phraseTokens.Length)
        {
            return false;
        }

        for (var index = 0; index <= haystackTokens.Length - phraseTokens.Length; index++)
        {
            var matches = true;
            for (var offset = 0; offset < phraseTokens.Length; offset++)
            {
                if (!string.Equals(haystackTokens[index + offset], phraseTokens[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNonNegatedSkillPhrase(string haystack, string phrase)
    {
        var haystackTokens = haystack
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var phraseTokens = phrase
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (phraseTokens.Length == 0 || haystackTokens.Length < phraseTokens.Length)
        {
            return false;
        }

        for (var index = 0; index <= haystackTokens.Length - phraseTokens.Length; index++)
        {
            var matches = true;
            for (var offset = 0; offset < phraseTokens.Length; offset++)
            {
                if (!string.Equals(haystackTokens[index + offset], phraseTokens[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches && !HasNegationBefore(haystackTokens, index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNegationBefore(string[] tokens, int phraseStartIndex)
    {
        var start = Math.Max(0, phraseStartIndex - 4);
        for (var index = start; index < phraseStartIndex; index++)
        {
            if (tokens[index] is "no" or "not" or "without" or "missing" or "lacks" or "lack" or "lacking" or "never")
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> NormalizeSet(params string[] values) =>
        values.Select(Canonicalize).ToHashSet(StringComparer.Ordinal);

    private static string[] CanonicalArray(params string[] values) =>
        values.Select(Canonicalize).Distinct(StringComparer.Ordinal).ToArray();

    private sealed record EvidenceIndex(IReadOnlyDictionary<string, string> SkillKeys, string NormalizedText);
}
