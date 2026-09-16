terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# 1. Definir el proveedor y la región (US East 1 - Norte de Virginia)
provider "aws" {
  region = "us-east-1"
}

# 2. Búsqueda de la imagen oficial de Ubuntu 22.04 LTS
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"]

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*"]
  }
}

# 3. Configurar el Firewall (Security Group)
resource "aws_security_group" "goal_line_sg" {
  name        = "goal_line_backend_sg"
  description = "Permitir SSH y trafico HTTP para la API"

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] 
  }

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# 4. Creación de la instancia EC2
resource "aws_instance" "goal_line_server" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = "t3.micro" # Corregido a t3.micro para compatibilidad Free Tier
  
  # IMPORTANTE: Reemplaza con el nombre de tu Key Pair sin .pem
  key_name      = "goalline-ssh-key" 
  
  vpc_security_group_ids = [aws_security_group.goal_line_sg.id]

  # Script de automatización: Instalación de Docker al iniciar
  user_data = <<-EOF
              #!/bin/bash
              apt-get update -y
              apt-get install -y docker.io
              systemctl start docker
              systemctl enable docker
              usermod -aG docker ubuntu
              EOF

  tags = {
    Name        = "GoalLine-Backend-SRT"
    Environment = "Produccion"
    Project     = "PTR"
  }
}

# 5. Output de la IP Pública
output "ip_publica_servidor" {
  value       = aws_instance.goal_line_server.public_ip
  description = "IP Pública para acceder a tu servidor vía SSH"
}