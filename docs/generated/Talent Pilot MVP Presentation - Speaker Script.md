# Talent Pilot MVP Presentation - Speaker Script

Target duration: about 27 minutes.

Delivery principle: keep every slide tied to the same thesis - deterministic workflows control the process, AI agents accelerate evidence-heavy work, and humans keep final hiring decisions.

## Slide 1: Talent Pilot MVP

**Presenter cue:** Open with the thesis: rules where precision matters, AI where evidence work is slow.

- Time: 60 seconds.
- Keep the explanation simple: deterministic automation controls the process, AI accelerates evidence-heavy work, and humans keep decision control.
- Set the expectation that this is not an AI demo bolted onto a workflow tool. The MVP proves a balanced operating model.

## Slide 2: Why We Built It

**Slide message:** The hiring process has repeatable control problems and evidence-heavy judgment problems.

**Presenter cue:** Walk left to right through the four pain points, then end on the risk of generic AI overreach.

- Time: 60 seconds.
- Explain the four problems from left to right: handoffs drift, evidence fragments, matching repeats, and generic AI can overreach.
- Transition: the MVP response is to split the work into deterministic and AI-assisted lanes.

## Slide 3: MVP Goal

**Slide message:** Build the stable hiring operating system first, then add AI where it saves real time.

**Presenter cue:** Emphasize the sequence: deterministic workflow foundation first, AI acceleration second.

- Time: 70 seconds.
- The main idea: workflows and rules handle what should be predictable. AI handles language, evidence synthesis, retrieval, and explanation.

## Slide 4: One-Page Product Overview

**Slide message:** A controlled hiring lifecycle across internal users, candidates, admin configuration, and AI support.

**Presenter cue:** Use this as the map: actors, lifecycle, Admin Center, candidate portal, and AI layer.

- Time: 60 seconds.
- Use this slide as the product map. Point out actors, lifecycle, Admin Center controls, and AI support.

## Slide 5: The Operating Model

**Slide message:** Three lanes keep the product explainable: deterministic control, AI support, and human accountability.

**Presenter cue:** Walk the three lanes and repeat the simple frame: control, support, decision.

- Time: 75 seconds.
- Walk row by row. Deterministic controls are not optional; they are what make the AI layer safe enough for hiring workflows.

## Slide 6: Tech Stack Snapshot

**Slide message:** The MVP uses a practical enterprise stack with local LLM runtime and SQL-based vector search.

**Presenter cue:** Keep this quick: the stack is practical, deployable, and observable.

- Time: 60 seconds.
- Do not over-explain each tool. The point is that the stack is understandable, deployable, and observable.

## Slide 7: Deterministic Foundation

**Slide message:** Before AI can help, the application must know tenant, role, owner, state, and audit context.

**Presenter cue:** Anchor trust here: tenant, role, state, and audit controls make the AI layer safe.

- Time: 70 seconds.
- Emphasize that this is what makes the product more than a chat interface. It is a workflow system with controlled access and history.

## Slide 8: Workflow Automation

**Slide message:** Department routing places work into the right queue while code-owned transitions protect the process.

**Presenter cue:** Trace the routing path from request creation to PMO/recruiter ownership.

- Time: 75 seconds.
- Demo cue: pause on the routing path. Explain that admins configure routing, but unsafe transition editing is not exposed in the MVP.

## Slide 9: Hiring Pipeline Flow

**Slide message:** From public job discovery to final outcome, the system records evidence while people decide.

**Presenter cue:** Trace the candidate journey from public job browsing to final hiring outcome.

- Time: 70 seconds.
- Keep this high-level. The product is not only internal workflow; it includes the candidate portal and the application journey.

## Slide 10: Demo Cue: Admin Dashboard

**Slide message:** Management visibility across requests, posts, applications, interviews, offers, and attention items.

**Presenter cue:** Point to the funnel and attention cards as the management visibility layer.

- Time: 60 seconds.
- Point to the funnel and attention panel. This is the management control surface, not just a task list.

## Slide 11: Demo Cue: Admin Workflows

**Slide message:** Tenant admins configure routing while system workflow controls remain protected.

**Presenter cue:** Point to configurable routing, then explain why transition editing is protected.

- Time: 60 seconds.
- Explain that this is a deliberate MVP choice: configure routing, but do not allow arbitrary transition editing until a stronger rules engine exists.

## Slide 12: Where AI Enters

**Slide message:** AI is applied to specific jobs with controlled inputs, not as an unrestricted hidden operator.

**Presenter cue:** Group agents by job to be done, not by model novelty.

- Time: 80 seconds.
- Group the agents by job to be done. The key point is not the count alone; it is that every agent has a bounded purpose.

## Slide 13: AI Agent Architecture

**Slide message:** Application code prepares evidence and validates output; the LLM generates language or structured recommendations.

**Presenter cue:** Point to orchestration: code gathers evidence and validates output; the LLM writes.

- Time: 90 seconds.
- This is one of the core slides. Explain that the LLM is important, but it does not directly control workflow or database changes.

## Slide 14: AI Agents Are Not Hidden Decision Makers

**Slide message:** The MVP uses AI assistance with explicit boundaries and human ownership.

**Presenter cue:** Use this as the risk-control answer for judges and management.

- Time: 65 seconds.
- This slide addresses judging and management risk. The system prevents AI from becoming an unreviewed hiring actor.

## Slide 15: Vector Search Explained

**Slide message:** Embeddings let the system compare meaning, not just exact keywords.

**Presenter cue:** Define embeddings and cosine similarity with one plain hiring example.

- Time: 90 seconds.
- Use a simple example: a CV may say cloud-native React portal while a job says frontend engineer with Azure delivery. Vector search helps connect those meanings.

## Slide 16: How Vectors Help Hiring

**Slide message:** Document, profile, job, employee, and question-bank evidence become searchable decision support.

**Presenter cue:** Show vectors as reusable semantic memory across multiple agents.

- Time: 90 seconds.
- Explain that embeddings support multiple agents: bench matching, rediscovery, applicant ranking, RAG, and interview questions.

## Slide 17: RAG Chat

**Slide message:** The assistant retrieves permitted context, builds an evidence prompt, and generates cited read-only answers.

**Presenter cue:** Define RAG simply: retrieve first, then generate with citations.

- Time: 90 seconds.
- Define RAG briefly: retrieve first, then generate. The LLM answers from permitted evidence rather than open-ended memory.

## Slide 18: RAG Access And Controls

**Slide message:** The assistant is context-aware: PMO, recruiter, manager, and admin views are intentionally different.

**Presenter cue:** Emphasize that role and context checks happen before retrieval.

- Time: 60 seconds.
- Point out that a global permission is not enough. The backend also checks context type, role, tenant, and record visibility.

## Slide 19: Key AI Agent: Bench Matching

**Slide message:** For PMO review, code scores internal bench evidence and the LLM explains ranked recommendations.

**Presenter cue:** Point to the score mix first, then use the right panel to define each signal.

- Time: 90 seconds.
- Explain the score mix. The code computes each normalized signal and multiplies it by the weight on the left.
- Skill coverage is matched requested skills divided by total requested skills. Vector similarity compares the job and employee profile embeddings semantically. Experience fit compares employee years against the request min/max range.
- Availability, project relevance, and location fit keep the recommendation grounded in operational reality. The LLM explains the ranked result after code scoring; it does not choose the employee.

## Slide 20: Key AI Agent: Talent Rediscovery + Applicant Ranking

**Slide message:** The application reuses historical evidence and current application evidence before recruiters decide.

**Presenter cue:** Compare two reuse loops: past warm candidates and current active applicants.

- Time: 80 seconds.
- Talent Rediscovery finds warm candidates from past data. Applicant Ranking compares candidates already applied to a specific job post.

## Slide 21: Key AI Agent: Online Headhunting

**Slide message:** The agent discovers lead-only external candidates from approved sources and keeps conversion manual.

**Presenter cue:** Define Boolean/X-Ray search, then stress the lead-only boundary.

- Time: 90 seconds.
- Explain Boolean search and X-Ray search simply. The agent builds precise queries, calls approved providers, deduplicates, scores, enriches with LLM, then saves lead-only results for recruiter review.

## Slide 22: Key AI Agent: Interview Question Recommender

**Slide message:** Question-bank retrieval and vector ranking ground the LLM before it returns interviewer-facing JSON.

**Presenter cue:** Explain question bank retrieval, vector ranking, and LLM JSON output in that order.

- Time: 80 seconds.
- The agent helps interviewers prepare better questions, but it cannot submit feedback, hire, reject, or move the candidate.

## Slide 23: Proof Of Engineering Discipline

**Slide message:** The MVP records behavior and tests AI boundaries instead of treating model output as magic.

**Presenter cue:** Translate tests, logs, and auditability into trust.

- Time: 70 seconds.
- Mention that the latest focused AI test slice passed 43 tests. The important management point is observable behavior and guardrails.

## Slide 24: Business Value

**Slide message:** The balanced design makes hiring faster, more consistent, more explainable, and safer to scale.

**Presenter cue:** Tie the technical features back to time saved, consistency, and safer AI adoption.

- Time: 60 seconds.
- Translate the technical architecture into business outcomes: shorter review cycles, better reuse, safer AI, and more analytics-ready data.

## Slide 25: Roadmap And Closing

**Slide message:** Next phases extend automation carefully without weakening human decision control.

**Presenter cue:** Close by returning to the thesis: deterministic control plus AI acceleration.

- Time: 75 seconds.
- Close by returning to the thesis: deterministic control makes the platform reliable; AI agents make evidence-heavy hiring work faster.
