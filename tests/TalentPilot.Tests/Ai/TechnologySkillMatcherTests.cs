using TalentPilot.Application.Ai;

namespace TalentPilot.Tests.Ai;

public sealed class TechnologySkillMatcherTests
{
    [Fact]
    public void Assess_ClassifiesDotNetCandidateForPythonRoleWithoutInflatingFastApi()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Python", "FastAPI", "PostgreSQL", "REST APIs"],
            ["C#", ".NET Core", "ASP.NET Web API", "SQL Server", "REST APIs"]);

        AssertLevel(assessment, "Python", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "FastAPI", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "PostgreSQL", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "REST APIs", SkillMatchLevel.Exact);
        Assert.True(assessment.OverallScore < 0.70m);
        Assert.Contains("Python is required, but no direct Python evidence", TechnologySkillMatcher.BuildDirectEvidenceWarning(assessment));
    }

    [Fact]
    public void Assess_TreatsDotNetToJavaAsStrongerThanDotNetToPython()
    {
        var dotNetToPython = TechnologySkillMatcher.Assess(
            ["Python", "FastAPI", "PostgreSQL", "REST APIs"],
            ["C#", ".NET Core", "ASP.NET Web API", "SQL Server", "REST APIs"]);
        var dotNetToJava = TechnologySkillMatcher.Assess(
            ["Java", "Spring Boot", "Hibernate", "REST APIs"],
            ["C#", ".NET Core", "Entity Framework", "REST APIs"]);

        AssertLevel(dotNetToJava, "Java", SkillMatchLevel.Transferable);
        AssertLevel(dotNetToJava, "Spring Boot", SkillMatchLevel.Transferable);
        AssertLevel(dotNetToJava, "Hibernate", SkillMatchLevel.Transferable);
        AssertLevel(dotNetToJava, "REST APIs", SkillMatchLevel.Exact);
        Assert.True(dotNetToJava.OverallScore > dotNetToPython.OverallScore);
    }

    [Fact]
    public void Assess_ClassifiesReactCandidateForAngularRoleAsTransferable()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Angular", "TypeScript", "RxJS", "NgRx"],
            ["React", "Next.js", "TypeScript", "Redux"]);

        AssertLevel(assessment, "Angular", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "TypeScript", SkillMatchLevel.Exact);
        AssertLevel(assessment, "RxJS", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "NgRx", SkillMatchLevel.Transferable);
    }

    [Fact]
    public void Assess_DoesNotPromoteGeneralFrontendSkillsToReact()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["React", "TypeScript", "Redux"],
            ["HTML", "CSS", "JavaScript", "jQuery"],
            "Frontend web developer");

        AssertLevel(assessment, "React", SkillMatchLevel.BroadCategory);
        AssertLevel(assessment, "TypeScript", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Redux", SkillMatchLevel.WeakOrMissing);
        Assert.True(assessment.OverallScore < 0.35m);
    }

    [Fact]
    public void Assess_ClassifiesDotNetWebForDotNetDesktopAsTransferable()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["C#", ".NET Desktop", "WPF or WinForms", "SQL Server"],
            ["C#", "ASP.NET MVC", ".NET Core", "SQL Server"]);

        AssertLevel(assessment, "C#", SkillMatchLevel.Exact);
        AssertLevel(assessment, ".NET Desktop", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "WPF or WinForms", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "SQL Server", SkillMatchLevel.Exact);
    }

    [Fact]
    public void Assess_DoesNotTreatPythonAutomationAsPythonBackendFit()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Python", "Django or FastAPI", "REST APIs", "PostgreSQL"],
            ["Python scripting", "Selenium automation", "Excel automation", "Basic SQL"]);

        AssertLevel(assessment, "Python", SkillMatchLevel.Exact);
        AssertLevel(assessment, "Django or FastAPI", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "REST APIs", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "PostgreSQL", SkillMatchLevel.Transferable);
        Assert.True(assessment.OverallScore < 0.50m);
    }

    [Fact]
    public void Assess_DoesNotMatchJavaScriptAsJava()
    {
        var assessment = TechnologySkillMatcher.Assess(["Java"], ["JavaScript", "React"]);

        AssertLevel(assessment, "Java", SkillMatchLevel.WeakOrMissing);
    }

    [Fact]
    public void Assess_DoesNotExtractNegatedSkillEvidenceFromText()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["React", "Angular"],
            ["Java", "Spring Boot"],
            "Senior Java Developer with backend-only profile and no React evidence. Missing Angular.");

        AssertLevel(assessment, "React", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Angular", SkillMatchLevel.WeakOrMissing);
    }

    [Fact]
    public void Assess_DoesNotTreatRetailSalesAsSaasEnterpriseSales()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["B2B SaaS sales", "CRM pipeline management", "Quota ownership", "Product demos", "Enterprise clients", "Sales communication"],
            ["Retail sales", "Customer handling", "Basic lead follow-up"]);

        AssertLevel(assessment, "B2B SaaS sales", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "CRM pipeline management", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Quota ownership", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Product demos", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Enterprise clients", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Sales communication", SkillMatchLevel.Transferable);
        Assert.True(assessment.OverallScore < 0.20m);
    }

    [Fact]
    public void Assess_TreatsAccountManagementAsTransferableToCustomerSuccess()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Customer success", "Product adoption", "Renewal management", "Churn reduction", "Account health tracking", "Client communication"],
            ["Account management", "Client communication", "Relationship management", "Upselling"]);

        AssertLevel(assessment, "Customer success", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Product adoption", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Renewal management", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Churn reduction", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Account health tracking", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Client communication", SkillMatchLevel.Exact);
        Assert.InRange(assessment.OverallScore, 0.30m, 0.50m);
    }

    [Fact]
    public void Assess_DoesNotCallGeneralHrExecutiveATechnicalRecruiter()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Technical recruitment", "Software engineering hiring", "LinkedIn sourcing", "ATS usage", "Candidate screening"],
            ["HR operations", "Employee records", "Onboarding", "Policy coordination"]);

        AssertLevel(assessment, "Technical recruitment", SkillMatchLevel.BroadCategory);
        AssertLevel(assessment, "Software engineering hiring", SkillMatchLevel.BroadCategory);
        AssertLevel(assessment, "LinkedIn sourcing", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "ATS usage", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Candidate screening", SkillMatchLevel.WeakOrMissing);
        Assert.True(assessment.OverallScore < 0.20m);
    }

    [Fact]
    public void Assess_TreatsNonTechnicalRecruitmentAsTransferableToTechnicalRecruitment()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Technical recruitment", "Engineering role sourcing", "Candidate screening", "ATS usage", "LinkedIn Recruiter"],
            ["Sales recruitment", "Candidate screening", "Job posting", "Interview coordination", "ATS usage"]);

        AssertLevel(assessment, "Technical recruitment", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Engineering role sourcing", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Candidate screening", SkillMatchLevel.Exact);
        AssertLevel(assessment, "ATS usage", SkillMatchLevel.Exact);
        AssertLevel(assessment, "LinkedIn Recruiter", SkillMatchLevel.WeakOrMissing);
        Assert.InRange(assessment.OverallScore, 0.60m, 0.70m);
    }

    [Fact]
    public void Assess_DistinguishesAccountingFromFpaResponsibilities()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Budgeting", "Forecasting", "Variance analysis", "Financial modeling", "Management reporting"],
            ["Bookkeeping", "Accounts payable", "Accounts receivable", "Monthly closing", "Financial reporting"]);

        AssertLevel(assessment, "Budgeting", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Forecasting", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Variance analysis", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Financial modeling", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Management reporting", SkillMatchLevel.StrongAdjacent);
        Assert.True(assessment.OverallScore < 0.25m);
    }

    [Fact]
    public void Assess_TreatsAzureDevOpsAsTransferableForAwsDevOps()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["AWS", "Terraform", "Kubernetes", "CI/CD", "Monitoring"],
            ["Azure DevOps", "Terraform", "AKS", "GitHub Actions", "Prometheus/Grafana"]);

        AssertLevel(assessment, "AWS", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Terraform", SkillMatchLevel.Exact);
        AssertLevel(assessment, "Kubernetes", SkillMatchLevel.Exact);
        AssertLevel(assessment, "CI/CD", SkillMatchLevel.Exact);
        AssertLevel(assessment, "Monitoring", SkillMatchLevel.Exact);
        Assert.InRange(assessment.OverallScore, 0.90m, 0.95m);
    }

    [Fact]
    public void Assess_DoesNotTreatManualQaAsAutomationQa()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Automation testing", "Selenium or Playwright", "API testing", "CI test integration", "Regression automation"],
            ["Manual testing", "Test cases", "Regression testing", "UAT", "Jira"]);

        AssertLevel(assessment, "Automation testing", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Selenium or Playwright", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "API testing", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "CI test integration", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Regression automation", SkillMatchLevel.Transferable);
        Assert.InRange(assessment.OverallScore, 0.20m, 0.30m);
    }

    [Fact]
    public void Assess_DoesNotTreatOrganicSocialAsPerformanceMarketing()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Performance marketing", "Paid ads", "Google Ads", "Meta Ads", "ROAS tracking", "Campaign optimization", "Analytics"],
            ["Social media calendar", "Organic posts", "Canva creatives", "Community engagement"]);

        AssertLevel(assessment, "Performance marketing", SkillMatchLevel.BroadCategory);
        AssertLevel(assessment, "Paid ads", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Google Ads", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Meta Ads", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "ROAS tracking", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Campaign optimization", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Analytics", SkillMatchLevel.WeakOrMissing);
        Assert.True(assessment.OverallScore < 0.10m);
    }

    [Fact]
    public void Assess_DistinguishesProjectCoordinationFromSoftwareDeliveryOwnership()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Software project management", "Agile/Scrum", "Sprint planning", "Client communication", "Risk management", "Delivery ownership"],
            ["Project coordination", "Meeting notes", "Task follow-up", "Internal reporting"]);

        AssertLevel(assessment, "Software project management", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "Agile/Scrum", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Sprint planning", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Client communication", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Risk management", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Delivery ownership", SkillMatchLevel.WeakOrMissing);
        Assert.True(assessment.OverallScore < 0.15m);
    }

    [Fact]
    public void Assess_TreatsBusinessAnalysisAsTransferableToProductOwnerScope()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["Backlog management", "User stories", "Stakeholder communication", "Prioritization", "Sprint planning", "Acceptance criteria"],
            ["Requirement gathering", "User stories", "Client communication", "Documentation", "UAT support"]);

        AssertLevel(assessment, "Backlog management", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "User stories", SkillMatchLevel.Exact);
        AssertLevel(assessment, "Stakeholder communication", SkillMatchLevel.StrongAdjacent);
        AssertLevel(assessment, "Prioritization", SkillMatchLevel.BroadCategory);
        AssertLevel(assessment, "Sprint planning", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Acceptance criteria", SkillMatchLevel.StrongAdjacent);
        Assert.InRange(assessment.OverallScore, 0.60m, 0.70m);
    }

    [Fact]
    public void Assess_DistinguishesExcelReportingFromDataEngineering()
    {
        var assessment = TechnologySkillMatcher.Assess(
            ["BI development", "SQL analytics", "Data warehousing", "ETL", "Dashboard development"],
            ["Excel reporting", "Basic SQL", "Monthly operations dashboard"]);

        AssertLevel(assessment, "BI development", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "SQL analytics", SkillMatchLevel.StrongAdjacent);
        AssertLevel(assessment, "Data warehousing", SkillMatchLevel.Transferable);
        AssertLevel(assessment, "ETL", SkillMatchLevel.WeakOrMissing);
        AssertLevel(assessment, "Dashboard development", SkillMatchLevel.BroadCategory);
        Assert.True(assessment.OverallScore < 0.55m);
    }

    private static void AssertLevel(
        SkillMatchAssessment assessment,
        string requiredSkill,
        SkillMatchLevel expectedLevel)
    {
        var item = Assert.Single(assessment.Items, item =>
            string.Equals(item.RequiredSkill, requiredSkill, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedLevel, item.MatchLevel);
    }
}
