terraform {
  required_version = ">= 1.6"
  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
    local = {
      source  = "hashicorp/local"
      version = "~> 2.4"
    }
    null = {
      source  = "hashicorp/null"
      version = "~> 3.2"
    }
  }
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

provider "local" {}

# ============================================================
# VARIABLES
# ============================================================
variable "cloudflare_api_token" {
  description = "Cloudflare API Token with Zone:Read, DNS:Edit, Workers Scripts:Edit"
  type        = string
  sensitive   = true
}

variable "zone_name" {
  description = "Root domain (e.g., edrs.xyz)"
  type        = string
  default     = "edrs.xyz"
}

variable "subdomain" {
  description = "Subdomain for the platform (e.g., learn)"
  type        = string
  default     = "learn"
}

variable "vm_host" {
  description = "Public IP or hostname of your VM"
  type        = string
}

variable "ssh_public_key" {
  description = "SSH public key for VM access"
  type        = string
}

variable "ssh_private_key" {
  description = "SSH private key for GitHub Actions deploy"
  type        = string
  sensitive   = true
}

# ============================================================
# CLOUDFLARE: DNS + TUNNEL
# ============================================================
data "cloudflare_zone" "root" {
  name = var.zone_name
}

resource "cloudflare_record" "tunnel" {
  zone_id = data.cloudflare_zone.root.id
  name    = var.subdomain
  type    = "CNAME"
  value   = "${var.cloudflare_tunnel_id}.cfargotunnel.com"
  proxied = true
  ttl     = 1
}

resource "cloudflare_tunnel" "platform" {
  name         = "devops-learning-platform"
  account_id   = data.cloudflare_zone.root.account_id
  config_src   = "cloudflare"
}

resource "cloudflare_tunnel_config" "platform" {
  tunnel_id = cloudflare_tunnel.platform.id
  config = jsonencode({
    ingress = [
      {
        hostname = "${var.subdomain}.${var.zone_name}"
        service  = "http://localhost:8080"
        originRequest = {
          connectTimeout = "30s"
          noTLSVerify    = true
        }
      },
      {
        service = "http_status:404"
      }
    ]
  })
}

resource "cloudflare_zero_trust_access_application" "platform" {
  zone_id     = data.cloudflare_zone.root.id
  name        = "DevOps Learning Platform"
  domain      = "${var.subdomain}.${var.zone_name}"
  type        = "self_hosted"
  session_duration = "24h"
  auto_redirect_to_identity = false
}

# Output tunnel ID for GitHub Actions secret
output "cloudflare_tunnel_id" {
  value     = cloudflare_tunnel.platform.id
  sensitive = true
}

output "cloudflare_tunnel_token" {
  value     = cloudflare_tunnel.platform.tunnel_secret
  sensitive = true
}
