-- Enable extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "vector";

-- Core enums
CREATE TYPE node_type AS ENUM (
    'technology', 'topic', 'subtopic', 'skill', 'lesson', 
    'lab', 'question', 'exam', 'certification', 'project'
);

CREATE TYPE edge_type AS ENUM (
    'prereq', 'covers', 'maps_to', 'replaces', 'depends_on', 'part_of'
);

CREATE TYPE lab_type AS ENUM (
    'guided', 'practice', 'troubleshooting', 'challenge', 'project', 'certification'
);

CREATE TYPE lab_status AS ENUM (
    'pending', 'provisioning', 'running', 'completed', 'failed', 'expired', 'cancelled'
);

CREATE TYPE question_type AS ENUM (
    'single_choice', 'multiple_choice', 'true_false', 'ordering', 
    'matching', 'scenario', 'troubleshooting', 'code', 'config', 'practical'
);

CREATE TYPE difficulty AS ENUM ('beginner', 'intermediate', 'advanced', 'expert');

-- Knowledge Graph Nodes
CREATE TABLE knowledge_node (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    type node_type NOT NULL,
    slug TEXT NOT NULL,
    title TEXT NOT NULL,
    content JSONB,           -- markdown, metadata, versioned content
    version TEXT,            -- technology version this targets
    metadata JSONB DEFAULT '{}',  -- difficulty, competency, official_source_url, estimated_time, etc.
    parent_id UUID REFERENCES knowledge_node(id),
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now(),
    UNIQUE (type, slug, version)
);

CREATE INDEX idx_knowledge_node_parent ON knowledge_node(parent_id);
CREATE INDEX idx_knowledge_node_type ON knowledge_node(type);
CREATE INDEX idx_knowledge_node_slug ON knowledge_node(slug);
CREATE INDEX idx_knowledge_node_content_gin ON knowledge_node USING GIN (content);
CREATE INDEX idx_knowledge_node_metadata_gin ON knowledge_node USING GIN (metadata);

-- Knowledge Graph Edges
CREATE TABLE knowledge_edge (
    from_id UUID NOT NULL REFERENCES knowledge_node(id) ON DELETE CASCADE,
    to_id UUID NOT NULL REFERENCES knowledge_node(id) ON DELETE CASCADE,
    edge_type edge_type NOT NULL,
    weight INT DEFAULT 1,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (from_id, to_id, edge_type)
);

CREATE INDEX idx_knowledge_edge_to ON knowledge_edge(to_id);
CREATE INDEX idx_knowledge_edge_type ON knowledge_edge(edge_type);

-- Users & Auth (supplements ASP.NET Core Identity)
CREATE TABLE app_user (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    identity_user_id TEXT NOT NULL UNIQUE,  -- links to AspNetUsers.Id
    display_name TEXT,
    avatar_url TEXT,
    preferred_language TEXT DEFAULT 'es',
    timezone TEXT DEFAULT 'America/Mexico_City',
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE organization (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    owner_id UUID NOT NULL REFERENCES app_user(id),
    plan TEXT DEFAULT 'free',  -- free, pro, enterprise
    settings JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE organization_member (
    organization_id UUID NOT NULL REFERENCES organization(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    role TEXT NOT NULL DEFAULT 'member',  -- owner, admin, member
    joined_at TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (organization_id, user_id)
);

-- Learning Progress
CREATE TABLE user_progress (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    node_id UUID NOT NULL REFERENCES knowledge_node(id) ON DELETE CASCADE,
    status TEXT NOT NULL DEFAULT 'not_started',  -- not_started, in_progress, completed, mastered
    score NUMERIC(5,2),  -- 0-100
    attempts INT DEFAULT 0,
    time_spent_seconds BIGINT DEFAULT 0,
    last_accessed_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}',
    UNIQUE (user_id, node_id)
);

CREATE INDEX idx_user_progress_user ON user_progress(user_id);
CREATE INDEX idx_user_progress_node ON user_progress(node_id);
CREATE INDEX idx_user_progress_status ON user_progress(status);

-- Lab System
CREATE TABLE lab_environment (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    description TEXT,
    lab_type lab_type NOT NULL,
    base_image TEXT NOT NULL,  -- docker image reference
    docker_compose TEXT,       -- full compose YAML for complex labs
    resource_limits JSONB DEFAULT '{"cpus": "0.5", "memory": "512m", "pids": 100, "timeout_seconds": 1800}',
    validation_script TEXT,    -- script to run for auto-grading
    setup_script TEXT,         -- runs before student starts
    cleanup_script TEXT,       -- runs after
    metadata JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE lab_attempt (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    lab_environment_id UUID NOT NULL REFERENCES lab_environment(id),
    status lab_status NOT NULL DEFAULT 'pending',
    container_id TEXT,
    container_ip TEXT,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    score NUMERIC(5,2),
    evidence JSONB,            -- collected logs, outputs, screenshots
    validation_result JSONB,   -- PASS/WARNING/FAIL details
    metadata JSONB DEFAULT '{}'
);

CREATE INDEX idx_lab_attempt_user ON lab_attempt(user_id);
CREATE INDEX idx_lab_attempt_lab ON lab_attempt(lab_environment_id);
CREATE INDEX idx_lab_attempt_status ON lab_attempt(status);
CREATE INDEX idx_lab_attempt_expires ON lab_attempt(expires_at) WHERE expires_at IS NOT NULL;

-- Assessment System
CREATE TABLE question (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    technology TEXT NOT NULL,
    topic TEXT,
    subtopic TEXT,
    level TEXT NOT NULL,  -- basic, intermediate, advanced
    difficulty difficulty NOT NULL,
    competency TEXT,
    question_type question_type NOT NULL,
    prompt TEXT NOT NULL,           -- markdown
    options JSONB,                  -- for choice types
    correct_answer JSONB NOT NULL,  -- varies by type
    explanation TEXT,               -- markdown explanation
    official_source TEXT,           -- URL to official docs
    technology_version TEXT,
    tags TEXT[],
    metadata JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_question_tech_level ON question(technology, level);
CREATE INDEX idx_question_tags ON question USING GIN (tags);
CREATE INDEX idx_question_active ON question(is_active) WHERE is_active;

CREATE TABLE question_version (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    question_id UUID NOT NULL REFERENCES question(id) ON DELETE CASCADE,
    version INT NOT NULL,
    prompt TEXT NOT NULL,
    options JSONB,
    correct_answer JSONB NOT NULL,
    explanation TEXT,
    technology_version TEXT,
    changed_by UUID REFERENCES app_user(id),
    changed_at TIMESTAMPTZ DEFAULT now(),
    change_reason TEXT,
    UNIQUE (question_id, version)
);

CREATE TABLE exam (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    technology TEXT,
    slug TEXT NOT NULL UNIQUE,
    title TEXT NOT NULL,
    description TEXT,
    level TEXT,  -- basic, intermediate, advanced, final, super
    passing_score NUMERIC(5,2) DEFAULT 70,
    time_limit_minutes INT,
    question_count INT,
    question_selection JSONB,  -- rules for selecting questions
    metadata JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE exam_attempt (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    exam_id UUID NOT NULL REFERENCES exam(id),
    status TEXT NOT NULL DEFAULT 'in_progress',  -- in_progress, submitted, graded, reviewed
    score NUMERIC(5,2),
    max_score NUMERIC(5,2),
    started_at TIMESTAMPTZ DEFAULT now(),
    submitted_at TIMESTAMPTZ,
    graded_at TIMESTAMPTZ,
    answers JSONB,              -- user answers per question
    grading_details JSONB,      -- per-question scoring
    metadata JSONB DEFAULT '{}'
);

CREATE INDEX idx_exam_attempt_user ON exam_attempt(user_id);
CREATE INDEX idx_exam_attempt_exam ON exam_attempt(exam_id);
CREATE INDEX idx_exam_attempt_status ON exam_attempt(status);

-- Certification Mapping
CREATE TABLE certification (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    code TEXT NOT NULL UNIQUE,  -- e.g., CKAD, AWS-SAA, DCA
    name TEXT NOT NULL,
    vendor TEXT NOT NULL,
    url TEXT,
    version TEXT,
    domains JSONB,  -- official domains with weights
    metadata JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE certification_mapping (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    certification_id UUID NOT NULL REFERENCES certification(id) ON DELETE CASCADE,
    domain_name TEXT NOT NULL,
    domain_weight NUMERIC(5,2),
    node_id UUID NOT NULL REFERENCES knowledge_node(id),
    coverage_weight NUMERIC(5,2) DEFAULT 1,
    metadata JSONB DEFAULT '{}',
    UNIQUE (certification_id, domain_name, node_id)
);

-- Technology Watcher
CREATE TABLE technology_source (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    technology TEXT NOT NULL,
    source_type TEXT NOT NULL,  -- github_releases, docker_tags, rss, html, pypi, npm, api
    source_url TEXT NOT NULL,
    selectors JSONB,  -- for HTML scraping
    headers JSONB,    -- auth headers if needed
    schedule_cron TEXT DEFAULT '0 */6 * * *',  -- every 6 hours
    last_checked_at TIMESTAMPTZ,
    last_version TEXT,
    last_content_hash TEXT,
    is_active BOOLEAN DEFAULT true,
    metadata JSONB DEFAULT '{}',
    UNIQUE (technology, source_type, source_url)
);

CREATE TABLE technology_change (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    technology TEXT NOT NULL,
    source_id UUID REFERENCES technology_source(id),
    change_type TEXT NOT NULL,  -- new_version, deprecation, breaking_change, security, eol
    previous_version TEXT,
    new_version TEXT,
    summary TEXT,
    details JSONB,
    severity TEXT DEFAULT 'medium',  -- low, medium, high, critical
    detected_at TIMESTAMPTZ DEFAULT now(),
    processed_at TIMESTAMPTZ,
    status TEXT DEFAULT 'pending',  -- pending, analyzing, proposed, approved, rejected, applied
    metadata JSONB DEFAULT '{}'
);

CREATE INDEX idx_tech_change_tech ON technology_change(technology);
CREATE INDEX idx_tech_change_status ON technology_change(status);
CREATE INDEX idx_tech_change_detected ON technology_change(detected_at);

CREATE TABLE impact_assessment (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    change_id UUID NOT NULL REFERENCES technology_change(id) ON DELETE CASCADE,
    affected_nodes JSONB,  -- array of {node_id, impact_level, reason}
    affected_labs JSONB,
    affected_questions JSONB,
    affected_exams JSONB,
    affected_cert_mappings JSONB,
    overall_impact TEXT DEFAULT 'low',  -- low, medium, high, critical
    recommendation TEXT,
    assessed_at TIMESTAMPTZ DEFAULT now(),
    assessed_by UUID REFERENCES app_user(id)
);

CREATE TABLE update_proposal (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    change_id UUID NOT NULL REFERENCES technology_change(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    description TEXT,
    proposed_changes JSONB,  -- array of {node_id, action, new_content}
    status TEXT DEFAULT 'draft',  -- draft, under_review, approved, rejected, applied
    created_by UUID REFERENCES app_user(id),
    reviewed_by UUID REFERENCES app_user(id),
    created_at TIMESTAMPTZ DEFAULT now(),
    reviewed_at TIMESTAMPTZ,
    applied_at TIMESTAMPTZ
);

-- Lab Regression
CREATE TABLE lab_regression_suite (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    lab_environment_id UUID NOT NULL REFERENCES lab_environment(id),
    name TEXT NOT NULL,
    test_script TEXT NOT NULL,
    expected_result JSONB,
    metadata JSONB DEFAULT '{}',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE lab_regression_run (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    suite_id UUID NOT NULL REFERENCES lab_regression_suite(id) ON DELETE CASCADE,
    technology_version TEXT NOT NULL,
    status TEXT NOT NULL,  -- pass, warning, fail, error
    output TEXT,
    artifacts JSONB,
    started_at TIMESTAMPTZ DEFAULT now(),
    completed_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}'
);

CREATE INDEX idx_regression_run_suite ON lab_regression_run(suite_id);
CREATE INDEX idx_regression_run_version ON lab_regression_run(technology_version);
CREATE INDEX idx_regression_run_status ON lab_regression_run(status);

-- Insights & Analytics
CREATE TABLE user_skill (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    technology TEXT NOT NULL,
    topic TEXT,
    level TEXT NOT NULL,  -- basic, intermediate, advanced
    proficiency NUMERIC(5,2) DEFAULT 0,  -- 0-100
    confidence NUMERIC(5,2) DEFAULT 0,   -- 0-100, based on evidence quality
    evidence_count INT DEFAULT 0,
    last_assessed_at TIMESTAMPTZ,
    metadata JSONB DEFAULT '{}',
    UNIQUE (user_id, technology, topic, level)
);

CREATE TABLE insight (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    type TEXT NOT NULL,  -- weakness, strength, recommendation, milestone, certification_readiness
    title TEXT NOT NULL,
    description TEXT,
    related_node_ids UUID[],
    priority INT DEFAULT 5,  -- 1-10
    is_read BOOLEAN DEFAULT false,
    is_dismissed BOOLEAN DEFAULT false,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now(),
    expires_at TIMESTAMPTZ
);

CREATE INDEX idx_insight_user ON insight(user_id);
CREATE INDEX idx_insight_type ON insight(type);
CREATE INDEX idx_insight_priority ON insight(priority);

-- Audit & Notifications
CREATE TABLE audit_log (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID REFERENCES app_user(id),
    action TEXT NOT NULL,
    entity_type TEXT,
    entity_id UUID,
    old_value JSONB,
    new_value JSONB,
    ip_address INET,
    user_agent TEXT,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_audit_user ON audit_log(user_id);
CREATE INDEX idx_audit_entity ON audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_created ON audit_log(created_at);

CREATE TABLE notification (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES app_user(id) ON DELETE CASCADE,
    type TEXT NOT NULL,  -- info, warning, success, error, achievement
    title TEXT NOT NULL,
    message TEXT,
    action_url TEXT,
    is_read BOOLEAN DEFAULT false,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_notification_user_unread ON notification(user_id, is_read) WHERE NOT is_read;

-- Updated at trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_knowledge_node_updated_at BEFORE UPDATE ON knowledge_node FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_lab_environment_updated_at BEFORE UPDATE ON lab_environment FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_exam_updated_at BEFORE UPDATE ON exam FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_certification_updated_at BEFORE UPDATE ON certification FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_technology_source_updated_at BEFORE UPDATE ON technology_source FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_update_proposal_updated_at BEFORE UPDATE ON update_proposal FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_lab_regression_suite_updated_at BEFORE UPDATE ON lab_regression_suite FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
