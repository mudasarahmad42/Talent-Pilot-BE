from __future__ import annotations

from dataclasses import dataclass
from html import escape
from pathlib import Path
from textwrap import wrap
from typing import Iterable, Sequence


ROOT = Path(__file__).resolve().parent
OUT_DIR = ROOT / "svg"

COLORS = {
    "canvas": "#F6F8FB",
    "paper": "#FFFFFF",
    "ink": "#172033",
    "muted": "#536273",
    "border": "#CAD4DF",
    "grid": "#E3EAF2",
    "line": "#536273",
    "softLine": "#95A6BA",
    "navy": "#263C55",
    "client": "#E8F3E8",
    "clientDark": "#557B56",
    "service": "#DDF0FA",
    "serviceDark": "#246A93",
    "data": "#FFE5DF",
    "dataDark": "#BA4C3B",
    "async": "#FFF2CF",
    "asyncDark": "#A66A14",
    "ai": "#E9E4FA",
    "aiDark": "#6751A9",
    "external": "#FBE2E7",
    "externalDark": "#A33E52",
    "worker": "#E0F3EC",
    "workerDark": "#1F7A5C",
}


@dataclass(frozen=True)
class Node:
    key: str
    title: str
    body: tuple[str, ...] | str
    x: int
    y: int
    w: int
    h: int
    fill: str
    accent: str


@dataclass(frozen=True)
class Table:
    key: str
    title: str
    fields: tuple[str, ...] | str
    x: int
    y: int
    w: int
    accent: str

    @property
    def h(self) -> int:
        fields = (self.fields,) if isinstance(self.fields, str) else self.fields
        return 70 + len(fields) * 36 + 18


def lines(value: tuple[str, ...] | str) -> tuple[str, ...]:
    return (value,) if isinstance(value, str) else value


def clean(value: str) -> str:
    return escape(value, quote=True)


def point(item: Node | Table, side: str) -> tuple[int, int]:
    x, y, w, h = item.x, item.y, item.w, item.h
    return {
        "left": (x, y + h // 2),
        "right": (x + w, y + h // 2),
        "top": (x + w // 2, y),
        "bottom": (x + w // 2, y + h),
    }[side]


class Svg:
    def __init__(self, width: int, height: int, title: str, subtitle: str) -> None:
        self.width = width
        self.height = height
        self.title = title
        self.subtitle = subtitle
        self.parts: list[str] = []

    def add(self, raw: str) -> None:
        self.parts.append(raw)

    def text(
        self,
        x: int,
        y: int,
        value: str,
        cls: str = "body",
        fill: str | None = None,
        anchor: str | None = None,
    ) -> None:
        attrs = [f'x="{x}"', f'y="{y}"', f'class="{cls}"']
        if fill:
            attrs.append(f'fill="{fill}"')
        if anchor:
            attrs.append(f'text-anchor="{anchor}"')
        self.add(f"<text {' '.join(attrs)}>{clean(value)}</text>")

    def rect(
        self,
        x: int,
        y: int,
        w: int,
        h: int,
        fill: str,
        stroke: str | None = None,
        radius: int = 12,
        cls: str = "",
        opacity: float | None = None,
    ) -> None:
        attrs = [
            f'x="{x}"',
            f'y="{y}"',
            f'width="{w}"',
            f'height="{h}"',
            f'rx="{radius}"',
            f'fill="{fill}"',
        ]
        if stroke:
            attrs.append(f'stroke="{stroke}"')
        if cls:
            attrs.append(f'class="{cls}"')
        if opacity is not None:
            attrs.append(f'opacity="{opacity}"')
        self.add(f"<rect {' '.join(attrs)} />")

    def line(
        self,
        x1: int,
        y1: int,
        x2: int,
        y2: int,
        stroke: str = COLORS["border"],
        width: float = 1.5,
        dashed: bool = False,
        opacity: float = 1,
    ) -> None:
        attrs = [
            f'x1="{x1}"',
            f'y1="{y1}"',
            f'x2="{x2}"',
            f'y2="{y2}"',
            f'stroke="{stroke}"',
            f'stroke-width="{width}"',
        ]
        if dashed:
            attrs.append('stroke-dasharray="10 9"')
        if opacity != 1:
            attrs.append(f'opacity="{opacity}"')
        self.add(f"<line {' '.join(attrs)} />")

    def header(self) -> None:
        self.rect(0, 0, self.width, 130, COLORS["navy"], None, 0)
        self.text(58, 58, self.title, "title", "white")
        self.text(60, 96, self.subtitle, "subtitle", "#D6E0EA")
        self.rect(self.width - 360, 34, 270, 46, "#405B78", None, 23)
        self.text(self.width - 225, 63, "Generated June 5, 2026", "tag", "#EDF4FA", "middle")

    def footer(self, note: str) -> None:
        self.line(55, self.height - 70, self.width - 55, self.height - 70, COLORS["border"], 2)
        self.text(58, self.height - 38, note, "tiny", COLORS["muted"])

    def section(self, x: int, y: int, w: int, h: int, title: str) -> None:
        self.rect(x, y, w, h, COLORS["paper"], COLORS["border"], 18, "section")
        self.text(x + 26, y + 42, title, "section-title")
        self.line(x + 26, y + 64, x + w - 26, y + 64, COLORS["border"], 2)

    def wrap_text(
        self,
        x: int,
        y: int,
        value: str,
        max_w: int,
        font_size: int = 17,
        cls: str = "body",
        fill: str | None = None,
        line_h: int | None = None,
        max_lines: int | None = None,
    ) -> int:
        line_h = line_h or int(font_size * 1.5)
        chars = max(12, int(max_w / (font_size * 0.52)))
        wrapped = wrap(value, width=chars, break_long_words=False) or [""]
        if max_lines and len(wrapped) > max_lines:
            wrapped = wrapped[:max_lines]
            wrapped[-1] = wrapped[-1].rstrip(".") + "..."
        for line in wrapped:
            self.text(x, y, line, cls, fill)
            y += line_h
        return y

    def box(self, n: Node) -> None:
        self.rect(n.x, n.y, n.w, n.h, COLORS[n.fill], COLORS["border"], 14, "box")
        self.rect(n.x, n.y, n.w, 60, COLORS[n.accent], None, 14)
        self.add(f'<rect x="{n.x}" y="{n.y + 38}" width="{n.w}" height="22" fill="{COLORS[n.accent]}" />')
        self.wrap_text(n.x + 22, n.y + 37, n.title, n.w - 44, 18, "box-title", "white", 20, 2)
        y = n.y + 88
        for body_line in lines(n.body):
            if not body_line:
                continue
            y = self.wrap_text(n.x + 22, y, body_line, n.w - 44, 16, "body", COLORS["ink"], 24, 5)
            y += 6

    def table(self, t: Table) -> None:
        self.rect(t.x, t.y, t.w, t.h, COLORS["paper"], COLORS["border"], 12, "box")
        self.rect(t.x, t.y, t.w, 58, COLORS[t.accent], None, 12)
        self.add(f'<rect x="{t.x}" y="{t.y + 38}" width="{t.w}" height="20" fill="{COLORS[t.accent]}" />')
        self.wrap_text(t.x + 18, t.y + 36, t.title, t.w - 36, 18, "box-title", "white", 20, 2)
        y = t.y + 86
        for field in lines(t.fields):
            self.text(t.x + 18, y, field, "table-field", COLORS["ink"])
            self.line(t.x + 16, y + 14, t.x + t.w - 16, y + 14, "#E8EEF5", 1)
            y += 36

    def connect(
        self,
        pts: Sequence[tuple[int, int]],
        label: str = "",
        *,
        dashed: bool = False,
        soft: bool = False,
        width: float = 3,
        opacity: float = 1,
        label_at: tuple[int, int] | None = None,
    ) -> None:
        stroke = COLORS["softLine"] if soft else COLORS["line"]
        d = " ".join(("M" if i == 0 else "L") + f"{x},{y}" for i, (x, y) in enumerate(pts))
        attrs = [
            f'd="{d}"',
            f'stroke="{stroke}"',
            f'stroke-width="{width}"',
            'fill="none"',
            'marker-end="url(#arrow)"',
        ]
        if dashed:
            attrs.append('stroke-dasharray="14 12"')
        if opacity != 1:
            attrs.append(f'opacity="{opacity}"')
        self.add(f"<path {' '.join(attrs)} />")
        if label:
            x, y = label_at or pts[len(pts) // 2]
            label_w = max(86, min(360, int(len(label) * 9 + 28)))
            self.rect(x - label_w // 2, y - 23, label_w, 34, "white", "#D9E0E8", 8)
            self.text(x, y, label, "label", COLORS["ink"], "middle")

    def to_string(self) -> str:
        body = "\n".join(self.parts)
        return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{self.width}" height="{self.height}" viewBox="0 0 {self.width} {self.height}" role="img" aria-labelledby="title desc">
<title id="title">{clean(self.title)}</title>
<desc id="desc">{clean(self.subtitle)}</desc>
<defs>
  <marker id="arrow" markerWidth="12" markerHeight="12" refX="10" refY="4" orient="auto" markerUnits="strokeWidth">
    <path d="M0,0 L0,8 L10,4 z" fill="{COLORS["line"]}" />
  </marker>
  <style>
    svg {{ background: {COLORS["canvas"]}; }}
    text {{ font-family: "Segoe UI", Arial, sans-serif; dominant-baseline: alphabetic; }}
    .title {{ font-size: 45px; font-weight: 760; letter-spacing: 0; }}
    .subtitle {{ font-size: 21px; font-weight: 450; }}
    .tag {{ font-size: 15px; font-weight: 650; }}
    .section-title {{ font-size: 24px; font-weight: 760; fill: {COLORS["ink"]}; }}
    .box-title {{ font-size: 18px; font-weight: 760; }}
    .body {{ font-size: 16px; fill: {COLORS["ink"]}; }}
    .label {{ font-size: 15px; font-weight: 760; }}
    .tiny {{ font-size: 14px; }}
    .table-field {{ font-size: 15px; fill: {COLORS["ink"]}; }}
    .section {{ stroke-width: 1.8; }}
    .box {{ stroke-width: 1.5; filter: drop-shadow(0 2px 2px rgba(23,32,51,.08)); }}
  </style>
</defs>
{body}
</svg>
'''

    def save(self, name: str) -> Path:
        path = OUT_DIR / name
        path.write_text(self.to_string(), encoding="utf-8")
        return path


def render_system_design_flow() -> Path:
    svg = Svg(
        4200,
        2600,
        "Talent Pilot System Design Flow",
        "Expanded runtime architecture: frontend, backend API, hosted queue, worker process, Ollama, SQL, files, and providers",
    )
    svg.header()
    sections = [
        (70, 190, 560, 1540, "Clients"),
        (750, 190, 650, 1540, "Frontend"),
        (1530, 190, 870, 1540, "Backend API process"),
        (2530, 190, 620, 1540, "Async and workers"),
        (3310, 190, 820, 1540, "Data and providers"),
        (70, 1860, 4060, 510, "Main communication flow"),
    ]
    for section in sections:
        svg.section(*section)
    for x, label in [(690, "browser boundary"), (1465, "HTTP boundary"), (2470, "async boundary"), (3225, "integration boundary")]:
        svg.rect(x - 14, 330, 28, 1180, "#111827", None, 12)
        svg.text(x, 1560, label, "tiny", COLORS["muted"], "middle")

    n = {
        "staff": Node("staff", "Internal staff UI", ("Tenant Admin, Presales, PMO, Recruiter, Hiring Manager, Interviewer"), 130, 350, 440, 140, "client", "clientDark"),
        "candidate": Node("candidate", "Candidate portal UI", ("Public jobs, authenticated apply, my applications"), 130, 660, 440, 130, "client", "clientDark"),
        "browser": Node("browser", "Browser session", ("Runs Angular SPA", "JWT attached by auth interceptor"), 130, 970, 440, 130, "client", "clientDark"),
        "angular": Node("angular", "Angular dev server", ("node.exe ng serve", "localhost:4200", "Serves SPA and HMR"), 820, 350, 510, 150, "service", "serviceDark"),
        "frontServices": Node("frontServices", "Frontend services", ("ApiService", "Auth interceptor", "RealtimeNotificationService"), 820, 680, 510, 160, "service", "serviceDark"),
        "guards": Node("guards", "Route guards and stores", ("Role-aware routes", "Feature state and DTO facades"), 820, 1010, 510, 145, "service", "serviceDark"),
        "api": Node("api", "ASP.NET Core API", ("TalentPilot.Api.exe", "localhost:5058", "Controllers + SignalR hub"), 1620, 320, 690, 155, "service", "serviceDark"),
        "auth": Node("auth", "Auth and tenant context", ("JWT validation", "Refresh token lifecycle", "Current user claims"), 1620, 650, 310, 165, "service", "serviceDark"),
        "app": Node("app", "Application services", ("Workflow use cases", "Authorization decisions", "Notification publishing"), 2000, 650, 310, 165, "service", "serviceDark"),
        "agents": Node("agents", "Code-owned AI agents", ("JD draft, CV parse, matching, RAG, interview questions, headhunting"), 1620, 980, 690, 160, "ai", "aiDark"),
        "repos": Node("repos", "Dapper repositories", ("Thin SQL access layer", "Transactions and stored procedures"), 1620, 1290, 690, 150, "data", "dataDark"),
        "channel": Node("channel", "In-memory channel", ("Online headhunting queue", "Inside API process; not durable across restart"), 2600, 335, 480, 150, "async", "asyncDark"),
        "hosted": Node("hosted", "Hosted service", ("OnlineHeadhuntingBackgroundService", "Consumes channel in API process"), 2600, 645, 480, 150, "ai", "aiDark"),
        "worker": Node("worker", "TalentPilot.Worker", ("Separate dotnet process", "Polls NotificationOutbox every 30s", "No inbound port"), 2600, 965, 480, 165, "worker", "workerDark"),
        "outbox": Node("outbox", "NotificationOutbox", ("SQL-backed durable email queue", "Pending, Processing, Sent, Failed"), 2600, 1290, 480, 150, "async", "asyncDark"),
        "providers": Node("providers", "External providers", ("Resend or Microsoft Graph email", "Tavily web search", "Google Calendar OAuth/events"), 3390, 335, 660, 165, "external", "externalDark"),
        "files": Node("files", "Local document store", ("Uploaded CV/application files", "SQL stores metadata"), 3390, 630, 310, 145, "data", "dataDark"),
        "ollama": Node("ollama", "Ollama", ("localhost:11434", "/api/generate", "/api/embeddings"), 3740, 845, 310, 145, "ai", "aiDark"),
        "sql": Node("sql", "SQL Server TalentPilot", ("Tenant/Auth", "Recruiting/Workflow", "Notifications/Outbox", "AI logs + VECTOR(768)"), 3390, 1135, 660, 180, "data", "dataDark"),
        "processes": Node("processes", "Local process count", ("6 OS processes observed: npm wrapper, Angular server, dotnet API wrapper, API host, worker, Ollama", "Plus 1 hosted service inside API"), 3390, 1455, 660, 150, "paper", "navy"),
    }
    for node in n.values():
        svg.box(node)

    svg.connect([point(n["staff"], "right"), (675, 420), (820, 425)], "uses", label_at=(680, 395))
    svg.connect([point(n["candidate"], "right"), (675, 725), (820, 425)], "uses", label_at=(690, 705))
    svg.connect([point(n["browser"], "right"), (675, 1035), point(n["frontServices"], "left")], "SPA runtime", label_at=(700, 1005))
    svg.connect([point(n["angular"], "bottom"), point(n["frontServices"], "top")], "assets", label_at=(1075, 590))
    svg.connect([point(n["frontServices"], "bottom"), point(n["guards"], "top")], "state", label_at=(1075, 925))
    svg.connect([point(n["frontServices"], "right"), (1465, 760), point(n["api"], "left")], "REST JSON + JWT", label_at=(1460, 725))
    svg.connect([point(n["frontServices"], "right"), (1465, 840), (1465, 430), point(n["api"], "left")], "SignalR", soft=True, label_at=(1488, 470))
    svg.connect([point(n["api"], "bottom"), (1965, 555), point(n["auth"], "top")], "auth", label_at=(1840, 565))
    svg.connect([point(n["api"], "bottom"), (1965, 555), point(n["app"], "top")], "DTO", label_at=(2120, 565))
    svg.connect([point(n["app"], "bottom"), (2155, 905), point(n["agents"], "top")], "AI use cases", label_at=(2180, 920))
    svg.connect([point(n["app"], "bottom"), (2185, 905), (2185, 1220), point(n["repos"], "top")], "SQL use cases", label_at=(2210, 1230))
    svg.connect([point(n["app"], "right"), (2470, 735), (2600, 410)], "enqueue search", label_at=(2495, 700))
    svg.connect([point(n["channel"], "bottom"), point(n["hosted"], "top")], "consume", label_at=(2840, 575))
    svg.connect([point(n["app"], "right"), (2470, 735), (2470, 1365), point(n["outbox"], "left")], "queue email rows", label_at=(2500, 1230))
    svg.connect([point(n["worker"], "bottom"), point(n["outbox"], "top")], "poll", label_at=(2840, 1215))
    svg.connect([point(n["hosted"], "right"), (3225, 720), point(n["providers"], "left")], "Tavily search", dashed=True, label_at=(3235, 670))
    svg.connect([point(n["worker"], "right"), (3225, 1047), (3225, 430), point(n["providers"], "left")], "send email", dashed=True, label_at=(3255, 1000))
    svg.connect([point(n["api"], "right"), (2470, 395), (2470, 250), (3300, 250), (3300, 420), point(n["providers"], "left")], "Calendar OAuth", dashed=True, label_at=(2890, 235))
    svg.connect([point(n["repos"], "right"), (3225, 1365), (3225, 1225), point(n["sql"], "left")], "Dapper SQL", label_at=(3235, 1325))
    svg.connect([point(n["agents"], "right"), (3225, 1060), (3225, 918), point(n["ollama"], "left")], "LLM + embeddings", dashed=True, label_at=(3245, 985))
    svg.connect([point(n["repos"], "right"), (3225, 1365), (3225, 702), point(n["files"], "left")], "documents", soft=True, label_at=(3245, 745))

    steps = [
        ("1", "Browser calls API", "REST JSON requests carry JWT; SignalR uses the same token."),
        ("2", "API validates context", "Controllers pass DTOs to services with tenant/user context."),
        ("3", "Services transact in SQL", "Dapper persists business records, audit, notifications, and AI logs."),
        ("4", "Realtime path", "SignalR publishes NotificationReceived immediately."),
        ("5", "Async email path", "Worker claims outbox rows and sends via provider."),
        ("6", "AI path", "Agents call Ollama and persist runs plus vectors."),
    ]
    x = 135
    for idx, title, body in steps:
        svg.rect(x, 1980, 610, 170, "white", COLORS["border"], 14, "box")
        svg.rect(x + 26, 2018, 48, 48, COLORS["navy"], None, 24)
        svg.text(x + 50, 2049, idx, "box-title", "white", "middle")
        svg.text(x + 96, 2048, title, "section-title")
        svg.wrap_text(x + 96, 2085, body, 450, 16, "body", COLORS["muted"], 24, 3)
        if idx != "6":
            svg.connect([(x + 610, 2065), (x + 660, 2065)], "")
        x += 655

    svg.footer("Expanded SVG layout: wider lanes, routed elbows, and separated labels to avoid card/arrow overlap.")
    return svg.save("01-system-design-flow.svg")


def render_notification_flow() -> Path:
    svg = Svg(
        3300,
        2000,
        "Talent Pilot Request, Realtime, and Worker Flow",
        "Expanded sequence flow through HTTP, SQL transaction, SignalR, durable outbox, and email delivery",
    )
    svg.header()
    actors = [
        ("Angular UI", 210, "client", "clientDark"),
        ("API Controller", 690, "service", "serviceDark"),
        ("Application Service", 1170, "service", "serviceDark"),
        ("SQL Server", 1650, "data", "dataDark"),
        ("SignalR Hub", 2130, "service", "serviceDark"),
        ("Worker", 2610, "worker", "workerDark"),
        ("Email Provider", 3090, "external", "externalDark"),
    ]
    x_map = {}
    for title, x, fill, accent in actors:
        x_map[title] = x
        svg.box(Node(title, title, "", x - 165, 200, 330, 90, fill, accent))
        svg.line(x, 325, x, 1500, COLORS["border"], 2, dashed=True)

    messages = [
        ("Angular UI", "API Controller", "POST workflow action", 410),
        ("API Controller", "Application Service", "Validate DTO and current user", 540),
        ("Application Service", "SQL Server", "Transaction: business row + audit", 670),
        ("Application Service", "SQL Server", "Insert NotificationRecipients", 800),
        ("Application Service", "SignalR Hub", "Publish NotificationReceived", 930),
        ("SignalR Hub", "Angular UI", "Realtime toast + store update", 1060),
        ("Application Service", "SQL Server", "Insert NotificationOutbox email rows", 1190),
        ("Worker", "SQL Server", "Poll and claim pending rows every 30s", 1320),
        ("Worker", "Email Provider", "Send email and mark Sent/Failed", 1450),
    ]
    for src, dst, label, y in messages:
        reverse = x_map[dst] < x_map[src]
        svg.connect([(x_map[src], y), (x_map[dst], y)], label, dashed=reverse or src == "Worker" or dst == "Email Provider", soft=reverse, label_at=((x_map[src] + x_map[dst]) // 2, y - 18))

    notes = [
        Node("sync", "Synchronous HTTP path", "Browser waits for controller/service/repository work to complete before the API response.", 120, 1640, 850, 140, "paper", "serviceDark"),
        Node("rt", "Realtime path", "SignalR is immediate. SQL NotificationRecipients remains durable read state after refresh/login.", 1225, 1640, 850, 140, "paper", "serviceDark"),
        Node("email", "Email path", "Email is asynchronous. Business actions do not depend on email provider availability.", 2330, 1640, 850, 140, "paper", "workerDark"),
    ]
    for note in notes:
        svg.box(note)

    svg.footer("TalentPilot.Worker has no inbound HTTP listener; it only polls the SQL-backed NotificationOutbox.")
    return svg.save("02-request-realtime-notification-worker-flow.svg")


def render_ai_flow() -> Path:
    svg = Svg(
        3900,
        2450,
        "Talent Pilot AI, Ollama, Vector, RAG, and Headhunting Flow",
        "Expanded AI architecture with backend-owned agents, local Ollama, vector storage, RAG evidence, and human review boundaries",
    )
    svg.header()
    for section in [
        (70, 190, 520, 1150, "Triggering UI actions"),
        (730, 190, 850, 1150, "Backend AI orchestration"),
        (1730, 190, 760, 1150, "Runtime providers"),
        (2640, 190, 1190, 1150, "Persistent AI evidence"),
        (70, 1520, 3760, 560, "Agent-specific flows"),
    ]:
        svg.section(*section)

    n = {
        "ui": Node("ui", "User action", "Draft JD, parse CV, rank bench, rediscover candidates, generate questions, ask RAG assistant, start online headhunting", 120, 350, 420, 220, "client", "clientDark"),
        "api": Node("api", "Operations / AI Assistant API", ("Receives structured input", "No open-ended workflow decision endpoint"), 800, 330, 700, 160, "service", "serviceDark"),
        "agents": Node("agents", "Code-owned agents", ("JobDescriptionDraftingAgent", "CvParserAgent", "BenchMatchingAgent", "TalentRediscoveryAgent", "ApplicantRankingAgent", "InterviewQuestionRecommendationAgent", "OnlineHeadhuntingAgent", "AiAssistantService"), 800, 650, 700, 300, "ai", "aiDark"),
        "guard": Node("guard", "Guardrails", ("Tenant context", "Human review required", "No auto reject", "No workflow movement by model output"), 800, 1080, 700, 170, "paper", "navy"),
        "ollama": Node("ollama", "Ollama runtime", ("localhost:11434", "llama3.2 text generation", "nomic-embed-text embeddings"), 1790, 330, 310, 170, "ai", "aiDark"),
        "tavily": Node("tavily", "Tavily search", ("Optional web research", "Quota guarded in SQL", "No private candidate search"), 2140, 330, 310, 170, "external", "externalDark"),
        "vector": Node("vector", "SQL Server VECTOR(768)", ("VectorEmbeddings", "Similarity search", "Source text hashes"), 1790, 730, 660, 160, "data", "dataDark"),
        "logs": Node("logs", "AI audit tables", ("AiAgentRuns", "AiRecommendationLogs", "OnlineCandidateSourcingRuns", "OnlineCandidateLeads"), 2700, 330, 490, 180, "data", "dataDark"),
        "rag": Node("rag", "RAG evidence", ("KnowledgeChunks", "AiAssistantConversations", "AiAssistantMessages", "Citations and feedback"), 3280, 330, 490, 180, "data", "dataDark"),
        "business": Node("business", "Business tables", ("JobRequests", "Employees", "Candidates", "Applications", "Interviews", "Question bank"), 2700, 735, 1070, 170, "data", "dataDark"),
        "channel": Node("channel", "In-memory headhunting channel", "API hosted background service consumes queued search jobs", 2700, 1080, 1070, 140, "async", "asyncDark"),
    }
    for node in n.values():
        svg.box(node)

    svg.connect([point(n["ui"], "right"), point(n["api"], "left")], "structured request", label_at=(660, 410))
    svg.connect([point(n["api"], "bottom"), point(n["agents"], "top")], "dispatch", label_at=(1150, 570))
    svg.connect([point(n["agents"], "bottom"), point(n["guard"], "top")], "enforce", label_at=(1150, 1020))
    svg.connect([point(n["agents"], "right"), (1640, 800), (1640, 590), (1945, 590), point(n["ollama"], "bottom")], "generate / embed", dashed=True, label_at=(1665, 610))
    svg.connect([point(n["agents"], "right"), (1640, 815), (1640, 620), (2295, 620), point(n["tavily"], "bottom")], "guarded search", dashed=True, label_at=(1835, 640))
    svg.connect([point(n["ollama"], "bottom"), (1945, 620), point(n["vector"], "top")], "768-d vectors", label_at=(1945, 650))
    svg.connect([point(n["agents"], "right"), (1625, 785), (1625, 270), (2700, 270), point(n["logs"], "left")], "run logs", label_at=(2180, 270))
    svg.connect([point(n["agents"], "right"), (1625, 800), (1625, 820), point(n["business"], "left")], "read evidence", label_at=(2465, 820))
    svg.connect([point(n["api"], "right"), (1625, 410), (1625, 1150), point(n["channel"], "left")], "queue online search", label_at=(1990, 1135))
    svg.connect([point(n["vector"], "right"), (2560, 810), (2560, 420), point(n["rag"], "left")], "retrieval context", label_at=(2950, 420))
    svg.connect([point(n["channel"], "left"), (1625, 1150), (1625, 800), point(n["agents"], "right")], "background run", dashed=True, soft=True, label_at=(1645, 1010))

    cards = [
        ("1. JD Draft", "Intake fields -> Ollama draft -> human edits -> AiAgentRuns logs metadata."),
        ("2. Ranking", "Backend loads controlled candidates/employees -> code score -> Ollama explains only."),
        ("3. RAG Assistant", "Embed query -> retrieve KnowledgeChunks -> answer with saved citations."),
        ("4. Online Headhunting", "Queue job -> Tavily under quota -> Ollama formats leads -> SQL persists review state."),
        ("5. Interview Questions", "Question bank and job evidence -> recommended set -> interviewer decides."),
    ]
    x = 130
    for title, body in cards:
        svg.box(Node(title, title, body, x, 1650, 690, 190, "paper", "aiDark"))
        x += 740

    svg.footer("AI output is advisory. Human users still claim, approve, reject, schedule, and close workflow decisions.")
    return svg.save("03-ai-ollama-vector-rag-flow.svg")


def render_recruiting_lifecycle_flow() -> Path:
    svg = Svg(
        5400,
        2700,
        "Talent Pilot Recruiting Lifecycle System Flow",
        "Expanded business workflow stages mapped to actors, UI, backend actions, SQL records, and async/AI side effects",
    )
    svg.header()
    rows = [
        ("Actors", 180, 410),
        ("Angular UI", 480, 720),
        ("Backend action", 790, 1080),
        ("Primary SQL records", 1160, 1450),
        ("Async / AI side effects", 1530, 1820),
    ]
    for title, y1, y2 in rows:
        svg.rect(70, y1, 5260, y2 - y1, COLORS["paper"], COLORS["border"], 16, "section")
        svg.text(95, y1 + 42, title, "section-title")

    stages = [
        ("Create request", "Presales / PMO", "Create Job Request", "POST job-requests", "JobRequests, JobRequestSkills, WorkflowAssignments", "Realtime + email outbox, requirement embedding"),
        ("PMO review", "PMO", "PMO Queue / PMO Review", "claim, rank, refer or forward", "Employees, EmployeeSkills, Referrals, AiRecommendationLogs", "Bench Matching Agent, optional Tavily context"),
        ("Presales decision", "Presales", "Job Request Detail", "accept/reject internal referral", "JobRequestEmployeeReferrals, JobRequestFulfillments", "Realtime + email decision notification"),
        ("Recruiter sourcing", "Recruiter", "Recruitment Queue / Sourcing", "create/publish post, invite candidates", "JobPosts, JobPostSkills, CandidateInvitations", "Talent Rediscovery, Online Headhunting, CV Parser"),
        ("Candidate pipeline", "Candidate + Recruiter + Interviewer", "Candidate portal + Interview tasks", "apply, screen, schedule, feedback", "JobApplications, Interviews, InterviewFeedback", "Calendar event, reminders, question recommender"),
        ("Hiring / offer", "Hiring Manager", "Hiring Manager Review", "generate offer, meeting, outcome", "OfferLetters, OfferPresentationMeetings", "Offer email and final outcome notification"),
        ("Fulfillment", "Backend closes request", "Dashboard / reporting", "insert fulfillment and close when filled", "JobRequestFulfillments, status history", "Dashboard and audit updates"),
    ]
    xs = [160, 900, 1640, 2380, 3120, 3860, 4600]
    w = 580
    for i, (stage, actor, ui, backend, sql, side) in enumerate(stages):
        x = xs[i]
        svg.text(x, 160, stage, "section-title")
        if i:
            svg.line(x - 55, 190, x - 55, 1830, COLORS["grid"], 2)
        svg.box(Node(f"a{i}", "Actor", actor, x, 250, w, 100, "client", "clientDark"))
        svg.box(Node(f"u{i}", "UI surface", ui, x, 550, w, 110, "service", "serviceDark"))
        svg.box(Node(f"b{i}", "Backend action", backend, x, 870, w, 130, "service", "serviceDark"))
        svg.box(Node(f"d{i}", "SQL records", sql, x, 1240, w, 145, "data", "dataDark"))
        svg.box(Node(f"s{i}", "Side effects", side, x, 1610, w, 145, "async", "asyncDark"))
    for i in range(len(xs) - 1):
        gap_x = (xs[i] + w + xs[i + 1]) // 2
        svg.connect([(xs[i] + w, 935), (xs[i] + w + 70, 935), (xs[i + 1] - 70, 935), (xs[i + 1], 935)], "next", label_at=(gap_x, 895))
        svg.connect([(xs[i] + w, 1312), (xs[i + 1], 1312)], "linked records", soft=True, label_at=(gap_x, 1272))

    svg.box(
        Node(
            "rules",
            "Workflow rules that matter",
            (
                "WorkflowAssignments move the Job Request baton. Interview tasks do not move the Job Request baton.",
                "AI agents rank, summarize, parse, or draft. Humans still claim, approve, reject, schedule, and close.",
                "NotificationRecipients support in-app/realtime state. NotificationOutbox supports asynchronous email.",
            ),
            160,
            2080,
            4820,
            190,
            "paper",
            "navy",
        )
    )
    svg.footer("Expanded lifecycle flow aligns with OperationsController endpoints and Dapper-backed SQL tables.")
    return svg.save("04-recruiting-lifecycle-system-flow.svg")


def render_core_erd_flow() -> Path:
    svg = Svg(
        5600,
        3600,
        "Talent Pilot Core ERD - Expanded Flow View",
        "Expanded table relationships grouped by tenant, identity, workforce, job request, publishing, and candidate pipeline",
    )
    svg.header()
    for section in [
        (70, 190, 720, 2840, "Tenant and identity"),
        (930, 190, 760, 2840, "Workforce and skills"),
        (1830, 190, 780, 2840, "Job request"),
        (2750, 190, 690, 2840, "Publishing"),
        (3580, 190, 1950, 2840, "Candidate pipeline"),
    ]:
        svg.section(*section)

    t = {
        "Tenants": Table("Tenants", "Tenants", ("PK TenantId", "Slug UK", "DisplayName", "Status"), 130, 340, 430, "clientDark"),
        "AppUsers": Table("AppUsers", "AppUsers", ("PK UserId", "FK TenantId", "Email UK", "AccountStatus"), 130, 780, 430, "serviceDark"),
        "Roles": Table("Roles", "Roles", ("PK RoleId", "FK TenantId", "Code UK", "Priority"), 130, 1230, 430, "workerDark"),
        "Groups": Table("Groups", "Groups", ("PK GroupId", "FK TenantId", "Purpose", "Name"), 130, 1680, 430, "asyncDark"),
        "Departments": Table("Departments", "Departments", ("PK DepartmentId", "FK TenantId", "FK LeadUserId", "Code UK"), 990, 340, 470, "serviceDark"),
        "Locations": Table("Locations", "Locations", ("PK LocationId", "FK TenantId", "Code UK", "TimezoneId"), 990, 770, 470, "serviceDark"),
        "Skills": Table("Skills", "Skills", ("PK SkillId", "FK TenantId", "NormalizedName UK", "Category"), 990, 1210, 470, "serviceDark"),
        "Employees": Table("Employees", "Employees", ("PK EmployeeId", "FK AppUserId", "FK DepartmentId", "FK LocationId", "BenchStatus"), 990, 1660, 470, "workerDark"),
        "EmployeeSkills": Table("EmployeeSkills", "EmployeeSkills", ("PK/FK EmployeeId", "PK/FK SkillId", "SkillLevel", "YearsExperience"), 990, 2200, 470, "workerDark"),
        "JobRequests": Table("JobRequests", "JobRequests", ("PK JobRequestId", "FK TenantId", "FK DepartmentId", "FK LocationId", "FK CreatedByUserId", "RequestCode UK", "Status", "CurrentStageKey"), 1890, 430, 520, "serviceDark"),
        "JobRequestSkills": Table("JobRequestSkills", "JobRequestSkills", ("PK/FK JobRequestId", "PK/FK SkillId", "IsRequired", "Weight"), 1890, 1110, 520, "serviceDark"),
        "WorkflowAssignments": Table("WorkflowAssignments", "WorkflowAssignments", ("PK AssignmentId", "EntityType", "EntityId", "Assigned user/group/role", "AssignmentStatus"), 1890, 1570, 520, "aiDark"),
        "EmployeeReferrals": Table("EmployeeReferrals", "JobRequestEmployeeReferrals", ("PK ReferralId", "FK JobRequestId", "FK EmployeeId", "Status", "FitScore"), 1890, 2090, 520, "workerDark"),
        "JobPosts": Table("JobPosts", "JobPosts", ("PK JobPostId", "FK JobRequestId", "FK RecruiterOwnerUserId", "Status", "PublishedAtUtc"), 2810, 480, 490, "aiDark"),
        "JobPostSkills": Table("JobPostSkills", "JobPostSkills", ("PK/FK JobPostId", "PK/FK SkillId", "IsRequired", "Weight"), 2810, 1030, 490, "aiDark"),
        "Candidates": Table("Candidates", "Candidates", ("PK CandidateId", "FK AppUserId", "Email UK", "Status"), 3650, 340, 470, "workerDark"),
        "CandidateSkills": Table("CandidateSkills", "CandidateSkills", ("PK/FK CandidateId", "PK/FK SkillId", "SkillLevel", "YearsExperience"), 4910, 340, 470, "workerDark"),
        "JobApplications": Table("JobApplications", "JobApplications", ("PK ApplicationId", "FK JobRequestId", "FK JobPostId", "FK CandidateId", "CurrentStatus", "ApplicationVersion"), 3650, 960, 470, "externalDark"),
        "StatusHistory": Table("StatusHistory", "JobApplicationStatusHistory", ("PK HistoryId", "FK JobApplicationId", "FromStatus", "ToStatus"), 4910, 1040, 470, "externalDark"),
        "Interviews": Table("Interviews", "Interviews", ("PK InterviewId", "FK JobApplicationId", "FK InterviewerUserId", "StartsAtUtc", "Status"), 3650, 1580, 470, "asyncDark"),
        "InterviewFeedback": Table("InterviewFeedback", "InterviewFeedback", ("PK FeedbackId", "FK InterviewId", "FK SubmittedByUserId", "Recommendation"), 4910, 1660, 470, "asyncDark"),
        "OfferLetters": Table("OfferLetters", "OfferLetters", ("PK OfferLetterId", "FK JobApplicationId", "FK JobRequestId", "FK CandidateId", "Status"), 3650, 2260, 470, "workerDark"),
        "Fulfillments": Table("Fulfillments", "JobRequestFulfillments", ("PK FulfillmentId", "FK JobRequestId", "FK JobApplicationId", "FK EmployeeId/CandidateId", "FulfillmentType"), 4910, 2260, 470, "workerDark"),
    }
    for table in t.values():
        svg.table(table)

    rels = [
        ("Tenants", "AppUsers", "1:N", "bottom", "top"),
        ("Tenants", "Departments", "1:N", "right", "left"),
        ("Tenants", "Locations", "1:N", "right", "left"),
        ("Tenants", "Skills", "1:N", "right", "left"),
        ("AppUsers", "Roles", "via UserRoles", "bottom", "top"),
        ("AppUsers", "Groups", "via GroupMembers", "bottom", "top"),
        ("AppUsers", "Employees", "employee login", "right", "left"),
        ("AppUsers", "Candidates", "candidate login", "right", "left"),
        ("Departments", "JobRequests", "1:N", "right", "left"),
        ("Locations", "JobRequests", "1:N", "right", "left"),
        ("Skills", "JobRequestSkills", "1:N", "right", "left"),
        ("Skills", "EmployeeSkills", "1:N", "bottom", "top"),
        ("Employees", "EmployeeSkills", "1:N", "bottom", "top"),
        ("JobRequests", "JobRequestSkills", "1:N", "bottom", "top"),
        ("JobRequests", "WorkflowAssignments", "tracked by", "bottom", "top"),
        ("JobRequests", "EmployeeReferrals", "1:N", "bottom", "top"),
        ("Employees", "EmployeeReferrals", "1:N", "right", "left"),
        ("JobRequests", "JobPosts", "1:0..N", "right", "left"),
        ("JobPosts", "JobPostSkills", "1:N", "bottom", "top"),
        ("Skills", "JobPostSkills", "1:N", "right", "left"),
        ("Candidates", "CandidateSkills", "1:N", "right", "left"),
        ("Skills", "CandidateSkills", "1:N", "right", "left"),
        ("JobRequests", "JobApplications", "1:N", "right", "left"),
        ("JobPosts", "JobApplications", "1:N", "right", "left"),
        ("Candidates", "JobApplications", "1:N", "bottom", "top"),
        ("JobApplications", "StatusHistory", "1:N", "right", "left"),
        ("JobApplications", "Interviews", "1:N", "bottom", "top"),
        ("Interviews", "InterviewFeedback", "1:0..1", "right", "left"),
        ("JobApplications", "OfferLetters", "1:N", "bottom", "top"),
        ("JobApplications", "Fulfillments", "external", "right", "left"),
        ("EmployeeReferrals", "Fulfillments", "internal", "right", "left"),
    ]
    custom_routes: dict[tuple[str, str], tuple[list[tuple[int, int]], tuple[int, int]]] = {
        ("AppUsers", "Candidates"): ([(560, 896), (820, 896), (820, 285), (3580, 285), (3580, 456), (3650, 456)], (2260, 285)),
        ("AppUsers", "Employees"): ([(560, 896), (845, 896), (845, 1794), (990, 1794)], (845, 1345)),
        ("Skills", "JobPostSkills"): ([(1460, 1326), (1755, 1326), (1755, 2660), (2685, 2660), (2685, 1146), (2810, 1146)], (2220, 2660)),
        ("Skills", "CandidateSkills"): ([(1460, 1326), (1755, 1326), (1755, 2780), (4865, 2780), (4865, 456), (4910, 456)], (3300, 2780)),
        ("JobRequests", "JobApplications"): ([(2410, 618), (2685, 618), (2685, 900), (3520, 900), (3520, 1112), (3650, 1112)], (3140, 900)),
        ("JobPosts", "JobApplications"): ([(3300, 614), (3505, 614), (3505, 1112), (3650, 1112)], (3505, 865)),
        ("JobApplications", "Fulfillments"): ([(4120, 1112), (4545, 1112), (4545, 2394), (4910, 2394)], (4545, 1760)),
        ("EmployeeReferrals", "Fulfillments"): ([(2410, 2224), (2685, 2224), (2685, 2900), (4865, 2900), (4865, 2394), (4910, 2394)], (3740, 2900)),
    }
    for src, dst, label, src_side, dst_side in rels:
        optional = label.startswith("via") or label in {"candidate login", "employee login", "tracked by", "external", "internal", "1:0..1"}
        s = point(t[src], src_side)
        e = point(t[dst], dst_side)
        if (src, dst) in custom_routes:
            pts, label_at = custom_routes[(src, dst)]
        elif abs(s[1] - e[1]) > 80 and src_side in {"right", "left"} and dst_side in {"right", "left"}:
            mid_x = (s[0] + e[0]) // 2
            pts = [s, (mid_x, s[1]), (mid_x, e[1]), e]
            label_at = (mid_x, (s[1] + e[1]) // 2)
        else:
            pts = [s, e]
            label_at = ((s[0] + e[0]) // 2, (s[1] + e[1]) // 2)
        svg.connect(pts, label, dashed=optional, soft=optional, width=2.2, opacity=0.78, label_at=label_at)

    svg.box(
        Node(
            "legend",
            "ERD reading guide",
            (
                "TenantId scopes tenant-owned runtime/configuration tables.",
                "Solid lines are core parent-child dependencies; dotted lines are optional, junction, or polymorphic-style relationships.",
                "WorkflowAssignments uses EntityType + EntityId, so the logical link is shown even where SQL cannot enforce a normal FK.",
            ),
            130,
            3190,
            5210,
            170,
            "paper",
            "navy",
        )
    )
    svg.footer("Expanded ERD SVG. Full executable table definitions remain in scripts/schema and scripts/migrations.")
    return svg.save("05-core-erd-refined-flow.svg")


def render_contact_sheet(paths: Iterable[Path]) -> Path:
    svg = Svg(
        2300,
        1650,
        "Talent Pilot Expanded Vector Diagram Set",
        "Open the linked SVG files for the refined, expanded, zoomable architecture and ERD diagrams",
    )
    svg.header()
    cards = [
        ("01", "System Design Flow", "Runtime processes, frontend, API, hosted queue, worker, Ollama, SQL, files, and providers."),
        ("02", "Request / Realtime / Worker Flow", "HTTP, SQL transaction, SignalR realtime state, durable outbox, and email worker."),
        ("03", "AI / Ollama / Vector / RAG Flow", "Backend-owned agents, local Ollama runtime, vectors, RAG, Tavily, and audit evidence."),
        ("04", "Recruiting Lifecycle Flow", "Business stages mapped to actors, UI surfaces, backend actions, SQL records, and side effects."),
        ("05", "Core ERD Flow", "Tenant, identity, workforce, job request, publishing, candidate pipeline, and outcome tables."),
    ]
    positions = [(100, 220), (1180, 220), (100, 675), (1180, 675), (640, 1130)]
    lookup = {path.name[:2]: path for path in paths}
    for (num, title, desc), (x, y) in zip(cards, positions):
        svg.rect(x, y, 1020, 330, "white", COLORS["border"], 18, "box")
        svg.rect(x + 35, y + 45, 86, 86, COLORS["navy"], None, 20)
        svg.text(x + 78, y + 101, num, "title", "white", "middle")
        svg.text(x + 155, y + 78, title, "section-title")
        svg.wrap_text(x + 155, y + 125, desc, 760, 17, "body", COLORS["muted"], 25, 4)
        svg.text(x + 155, y + 250, lookup[num].name, "label", COLORS["serviceDark"])
    svg.footer("Expanded SVG files use selectable text and routed vector arrows. They are the recommended diagrams to embed or share.")
    return svg.save("00-refined-system-design-contact-sheet.svg")


def write_readme(paths: Sequence[Path], contact: Path) -> None:
    lines_out = [
        "# Refined System Design Diagrams - SVG",
        "",
        "Generated on June 5, 2026 from the current backend/frontend runtime and SQL schema.",
        "",
        "Use these expanded SVG files as the primary diagrams. They contain vector text, routed arrows, wider lanes, and scalable ERD tables, so they stay readable when zoomed or embedded in documents.",
        "",
        f"![Vector contact sheet]({contact.name})",
        "",
        "## Vector Images",
        "",
    ]
    for path in [contact, *paths]:
        lines_out.append(f"- [{path.name}]({path.name})")
    lines_out.extend(
        [
            "",
            "## Scope",
            "",
            "- `01-system-design-flow.svg` shows the full process, service, data, worker, Ollama, and provider architecture.",
            "- `02-request-realtime-notification-worker-flow.svg` shows synchronous HTTP, realtime SignalR, and asynchronous email outbox behavior.",
            "- `03-ai-ollama-vector-rag-flow.svg` shows local AI runtime, embeddings, RAG, and online headhunting flow.",
            "- `04-recruiting-lifecycle-system-flow.svg` maps business workflow stages to UI, API actions, SQL records, and async side effects.",
            "- `05-core-erd-refined-flow.svg` is the expanded ERD view for core recruiting tables.",
            "",
        ]
    )
    (OUT_DIR / "README.md").write_text("\n".join(lines_out), encoding="utf-8")


def write_root_readme() -> None:
    lines_out = [
        "# Talent Pilot System Design Diagrams",
        "",
        "Generated on June 5, 2026 from the current backend/frontend runtime and SQL schema.",
        "",
        "Use [`svg`](svg) as the primary diagram set. These files are expanded vector images with selectable text, scalable arrows, and scalable ERD tables.",
        "",
        "## Primary SVG Images",
        "",
        "- [`svg/00-refined-system-design-contact-sheet.svg`](svg/00-refined-system-design-contact-sheet.svg)",
        "- [`svg/01-system-design-flow.svg`](svg/01-system-design-flow.svg)",
        "- [`svg/02-request-realtime-notification-worker-flow.svg`](svg/02-request-realtime-notification-worker-flow.svg)",
        "- [`svg/03-ai-ollama-vector-rag-flow.svg`](svg/03-ai-ollama-vector-rag-flow.svg)",
        "- [`svg/04-recruiting-lifecycle-system-flow.svg`](svg/04-recruiting-lifecycle-system-flow.svg)",
        "- [`svg/05-core-erd-refined-flow.svg`](svg/05-core-erd-refined-flow.svg)",
        "",
    ]
    (ROOT / "README.md").write_text("\n".join(lines_out), encoding="utf-8")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    paths = [
        render_system_design_flow(),
        render_notification_flow(),
        render_ai_flow(),
        render_recruiting_lifecycle_flow(),
        render_core_erd_flow(),
    ]
    contact = render_contact_sheet(paths)
    write_readme(paths, contact)
    write_root_readme()
    print("Generated expanded SVG diagrams:")
    print(contact)
    for path in paths:
        print(path)


if __name__ == "__main__":
    main()
