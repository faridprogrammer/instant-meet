#!/bin/bash

# ==============================================================================
# InstantMeet Installation Script for Ubuntu
# ==============================================================================
# This script automates the deployment of the InstantMeet application.
# It performs the following steps:
#   1. Checks for required dependencies (.NET 8 SDK, Nginx) and installs them.
#   2. Builds and publishes the Web and CLI applications to a single directory.
#   3. Sets up the Web application to run as a systemd service.
#   4. Configures Nginx as a reverse proxy for the application on a specified port.
#   5. Configures the firewall (UFW) to allow web traffic on that port.
#
# USAGE:
#   1. Clone your git repository to the server.
#   2. Make this script executable: chmod +x install.sh
#   3. Run with sudo, providing the public domain name and an optional port.
#      sudo ./install.sh your-domain.com [port]
#
#      Example (default port 80):
#      sudo ./install.sh your-domain.com
#
#      Example (custom port 8080):
#      sudo ./install.sh your-domain.com 8080
# ==============================================================================

# --- Script Configuration ---

# The name for the service and configuration files.
# Keep it simple, no spaces or special characters.
APP_NAME="instantmeet"

# The directory where the published application will be installed.
INSTALL_DIR="/var/www/${APP_NAME}"

# The user and group that will run the web service.
# 'www-data' is the standard for web services on Ubuntu/Debian.
SERVICE_USER="www-data"

# The default port Kestrel (the ASP.NET Core server) will listen on.
# This is an internal port; Nginx will proxy traffic from the public port.
KESTREL_PORT=5000

# --- End of Configuration ---


# --- Helper Functions ---

# Function to print a formatted header message.
print_header() {
    echo ""
    echo "=============================================================================="
    echo "=> $1"
    echo "=============================================================================="
}

# Function to print an error message and exit.
die() {
    echo ""
    echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    echo "ERROR: $1"
    echo "Aborting installation."
    echo "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    exit 1
}


# --- Script Execution ---

# 1. Initial Checks & Argument Parsing
# ------------------------------------------------------------------------------

# Check if the script is run as root.
if [ "$EUID" -ne 0 ]; then
  die "This script must be run with sudo or as the root user."
fi

# Check for the domain name argument.
if [ -z "$1" ]; then
    die "Usage: sudo ./install.sh <your-domain.com> [port]"
fi
DOMAIN_NAME="$1"

# Use the second argument as the Nginx port, or default to 80.
NGINX_PORT="${2:-80}"

# Validate that the port is a valid number.
if ! [[ "$NGINX_PORT" =~ ^[0-9]+$ ]] || [ "$NGINX_PORT" -lt 1 ] || [ "$NGINX_PORT" -gt 65535 ]; then
    die "Invalid port number provided: '${NGINX_PORT}'. Must be a number between 1 and 65535."
fi

echo "--> Using Nginx port: ${NGINX_PORT}"


# Exit immediately if a command exits with a non-zero status.
set -e


# 2. Dependency Installation
# ------------------------------------------------------------------------------

print_header "Checking and Installing Dependencies"

# Install .NET 8 SDK if not already installed.
if ! command -v dotnet &> /dev/null; then
    echo "--> .NET SDK not found. Installing .NET 8 SDK..."
    # Add Microsoft package repository
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # Install SDK
    apt-get update
    apt-get install -y apt-transport-https
    apt-get install -y dotnet-sdk-8.0
    echo "--> .NET 8 SDK installed successfully."
else
    echo "--> .NET SDK is already installed. ($(dotnet --version))"
fi

# Install Nginx if not already installed.
if ! command -v nginx &> /dev/null; then
    echo "--> Nginx not found. Installing Nginx..."
    apt-get update
    apt-get install -y nginx
    echo "--> Nginx installed successfully."
else
    echo "--> Nginx is already installed."
fi


# 3. Build and Publish Application
# ------------------------------------------------------------------------------

print_header "Building and Publishing Applications"

# Define project paths relative to the script's location.
SCRIPT_DIR=$(dirname "$0")
WEB_PROJ_PATH="${SCRIPT_DIR}/sources/InstantMeet.Web/InstantMeet.Web.csproj"
CLI_PROJ_PATH="${SCRIPT_DIR}/sources/InstantMeet.CLI/InstantMeet.CLI.csproj"

# Verify that project files exist.
[ ! -f "$WEB_PROJ_PATH" ] && die "Web project file not found at: $WEB_PROJ_PATH"
[ ! -f "$CLI_PROJ_PATH" ] && die "CLI project file not found at: $CLI_PROJ_PATH"

echo "--> Creating installation directory: ${INSTALL_DIR}"
mkdir -p "$INSTALL_DIR"

# Publish both projects to the same output directory.
# 'publish' creates a self-contained, optimized build for deployment.
echo "--> Publishing Web Application..."
dotnet publish "$WEB_PROJ_PATH" --configuration Release --output "$INSTALL_DIR"

echo "--> Publishing CLI Application..."
dotnet publish "$CLI_PROJ_PATH" --configuration Release --output "$INSTALL_DIR"

# Set ownership of the installation directory to the service user.
# This is crucial for security and for the app to be able to write logs, etc.
chown -R "${SERVICE_USER}:${SERVICE_USER}" "$INSTALL_DIR"

echo "--> Build and publish complete."


# 4. Setup Systemd Service for the Web App
# ------------------------------------------------------------------------------

print_header "Configuring systemd service"

# The name of the main web application DLL.
# dotnet publish renames the entry point DLL to match the project name.
WEB_DLL_NAME="InstantMeet.Web.dll"

# Create the service file using a heredoc for readability.
cat <<EOF > /etc/systemd/system/${APP_NAME}.service
[Unit]
Description=InstantMeet Web Application
After=network.target

[Service]
WorkingDirectory=${INSTALL_DIR}
ExecStart=/usr/bin/dotnet ${INSTALL_DIR}/${WEB_DLL_NAME} --urls "http://localhost:${KESTREL_PORT}"
Restart=always
# Restart service after 10 seconds if it crashes
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=${APP_NAME}
User=${SERVICE_USER}
Group=${SERVICE_USER}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF

echo "--> Created service file: /etc/systemd/system/${APP_NAME}.service"

# Reload systemd to recognize the new service, then enable and start it.
echo "--> Enabling and starting the ${APP_NAME} service..."
systemctl daemon-reload
systemctl enable "${APP_NAME}.service"
systemctl start "${APP_NAME}.service"

echo "--> Service started. Check status with: systemctl status ${APP_NAME}"


# 5. Configure Nginx Reverse Proxy
# ------------------------------------------------------------------------------

print_header "Configuring Nginx reverse proxy on port ${NGINX_PORT}"

# Create the Nginx site configuration.
# This will proxy requests from the public domain to the internal Kestrel server.
CONFIG_FILE="/etc/nginx/sites-available/${APP_NAME}"

cat <<EOF > "$CONFIG_FILE"
server {
    listen ${NGINX_PORT};
    listen [::]:${NGINX_PORT};
    server_name ${DOMAIN_NAME};

    location / {
        proxy_pass http://localhost:${KESTREL_PORT};
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF

echo "--> Created Nginx config file: ${CONFIG_FILE}"

# Enable the site by creating a symbolic link.
# -f ensures that if we run the script again, it overwrites the old link.
ln -sf "$CONFIG_FILE" "/etc/nginx/sites-enabled/"

# Remove the default Nginx welcome page if it exists.
rm -f /etc/nginx/sites-enabled/default

# Test the Nginx configuration for syntax errors.
echo "--> Testing Nginx configuration..."
nginx -t

# Reload Nginx to apply the new configuration.
echo "--> Reloading Nginx..."
systemctl reload nginx


# 6. Configure Firewall (A crucial step!)
# ------------------------------------------------------------------------------

print_header "Configuring Firewall (UFW)"

if command -v ufw &> /dev/null; then
    echo "--> Allowing SSH and TCP traffic on port ${NGINX_PORT} through UFW..."
    ufw allow ssh
    ufw allow ${NGINX_PORT}/tcp
    ufw --force enable
    echo "--> Firewall enabled and configured."
else
    echo "--> WARNING: UFW (Uncomplicated Firewall) is not installed. It is highly recommended for security."
fi


# --- Completion ---
# ------------------------------------------------------------------------------

print_header "Installation Complete!"
echo ""
echo "The InstantMeet application has been successfully deployed."

# Adjust the final URL based on whether the port is the standard HTTP port 80
if [ "$NGINX_PORT" -eq 80 ]; then
    echo "Your web application should now be accessible at: http://${DOMAIN_NAME}"
else
    echo "Your web application should now be accessible at: http://${DOMAIN_NAME}:${NGINX_PORT}"
fi

echo ""
echo "--- How to use the CLI ---"
echo "The command-line tool is located at:"
echo "${INSTALL_DIR}/InstantMeet.CLI"
echo ""
echo "--- IMPORTANT NEXT STEPS ---"
echo "1. DNS: Make sure the A record for '${DOMAIN_NAME}' points to this server's IP address."
if [ "$NGINX_PORT" -eq 80 ]; then
    echo "2. HTTPS: For production, you MUST enable HTTPS. Use Certbot to get a free SSL certificate."
fi
echo ""
echo "--- Service Management ---"
echo "Check service status: sudo systemctl status ${APP_NAME}"
echo "View service logs:    sudo journalctl -fu ${APP_NAME} -e"
echo "Stop the service:     sudo systemctl stop ${APP_NAME}"
echo "Start the service:    sudo systemctl start ${APP_NAME}"
echo ""
