#!/bin/bash

# ==============================================================================
# InstanVoiceRoom Uninstallation Script for Ubuntu
# ==============================================================================
# This script completely removes the InstanVoiceRoom application and its
# associated configurations created by the install.sh script.
#
# It performs the following steps:
#   1. Stops and disables the systemd service.
#   2. Removes the systemd service file.
#   3. Removes the Nginx configuration and re-enables the default site.
#   4. Removes the application files from /var/www.
#   5. Deletes the firewall rule for the application's port.
#
# USAGE:
#   Run with sudo, providing the port that was used during installation.
#
#   Example (if installed on default port 80):
#   sudo ./uninstall.sh
#
#   Example (if installed on custom port 8080):
#   sudo ./uninstall.sh 8080
# ==============================================================================

# --- Script Configuration ---

# The name must match the one used in the installation script.
APP_NAME="instantmeet"

# The installation directory must match the one from the install script.
INSTALL_DIR="/var/www/${APP_NAME}"

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
    echo "Aborting uninstallation."
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

# Use the first argument as the Nginx port, or default to 80.
NGINX_PORT="${1:-80}"

echo "--> Preparing to uninstall ${APP_NAME} which was running on port ${NGINX_PORT}"


# 2. Confirmation Prompt
# ------------------------------------------------------------------------------
print_header "WARNING: This script will permanently delete files"
echo "This will remove the following:"
echo " - The systemd service: ${APP_NAME}.service"
echo " - The application directory: ${INSTALL_DIR}"
echo " - The Nginx config: /etc/nginx/sites-available/${APP_NAME}"
echo " - The firewall rule for port ${NGINX_PORT}"
echo ""
read -p "Are you sure you want to continue? (y/N): " CONFIRM
if [[ ! "$CONFIRM" =~ ^[yY]([eE][sS])?$ ]]; then
    echo "Uninstallation cancelled."
    exit 0
fi


# Exit immediately if a command exits with a non-zero status.
set -e

# 3. Stop and Disable the Service
# ------------------------------------------------------------------------------
print_header "Removing systemd service"

SERVICE_FILE="/etc/systemd/system/${APP_NAME}.service"

# Check if the service is active and stop it.
if systemctl is-active --quiet "${APP_NAME}.service"; then
    echo "--> Stopping the ${APP_NAME} service..."
    systemctl stop "${APP_NAME}.service"
else
    echo "--> Service is not currently running."
fi

# Check if the service is enabled and disable it.
if systemctl is-enabled --quiet "${APP_NAME}.service"; then
    echo "--> Disabling the ${APP_NAME} service from startup..."
    systemctl disable "${APP_NAME}.service"
else
    echo "--> Service was not enabled to start on boot."
fi

# Remove the service file if it exists.
if [ -f "$SERVICE_FILE" ]; then
    echo "--> Deleting service file: ${SERVICE_FILE}"
    rm "$SERVICE_FILE"
    echo "--> Reloading systemd daemon..."
    systemctl daemon-reload
else
    echo "--> Service file not found. Skipping."
fi


# 4. Remove Nginx Configuration
# ------------------------------------------------------------------------------
print_header "Removing Nginx Configuration"

CONFIG_FILE="/etc/nginx/sites-available/${APP_NAME}"
SYMBOLIC_LINK="/etc/nginx/sites-enabled/${APP_NAME}"

# Remove the symbolic link if it exists
if [ -L "$SYMBOLIC_LINK" ]; then
    echo "--> Removing Nginx site link: ${SYMBOLIC_LINK}"
    rm "$SYMBOLIC_LINK"
else
    echo "--> Nginx site link not found. Skipping."
fi

# Remove the configuration file if it exists
if [ -f "$CONFIG_FILE" ]; then
    echo "--> Removing Nginx config file: ${CONFIG_FILE}"
    rm "$CONFIG_FILE"
else
    echo "--> Nginx config file not found. Skipping."
fi

# Restore the default Nginx welcome page if it doesn't exist.
DEFAULT_NGINX_LINK="/etc/nginx/sites-enabled/default"
if [ ! -L "$DEFAULT_NGINX_LINK" ]; then
    echo "--> Re-enabling the default Nginx site."
    ln -s /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default
fi

echo "--> Testing Nginx configuration..."
nginx -t

echo "--> Reloading Nginx..."
systemctl reload nginx


# 5. Remove Application Files
# ------------------------------------------------------------------------------
print_header "Removing Application Files"

if [ -d "$INSTALL_DIR" ]; then
    echo "--> Deleting application directory: ${INSTALL_DIR}"
    rm -rf "$INSTALL_DIR"
    echo "--> Directory removed."
else
    echo "--> Application directory not found. Skipping."
fi


# 6. Update Firewall
# ------------------------------------------------------------------------------
print_header "Updating Firewall (UFW)"

if command -v ufw &> /dev/null; then
    # Check if the rule exists before trying to delete it.
    if ufw status | grep -q "${NGINX_PORT}/tcp"; then
        echo "--> Deleting firewall rule for port ${NGINX_PORT}/tcp..."
        ufw delete allow "${NGINX_PORT}/tcp"
        echo "--> Firewall rule removed."
    elif ufw status | grep -q "Nginx Full" && [ "$NGINX_PORT" -eq 80 ]; then
         echo "--> Deleting firewall rule for 'Nginx Full'..."
         ufw delete allow 'Nginx Full'
         echo "--> Firewall rule removed."
    else
        echo "--> Firewall rule for port ${NGINX_PORT} not found. Skipping."
    fi
else
    echo "--> UFW is not installed. Skipping firewall configuration."
fi

# --- Completion ---
# ------------------------------------------------------------------------------

print_header "Uninstallation Complete!"
echo ""
echo "The InstanVoiceRoom application and its configurations have been removed."
echo ""
echo "--- IMPORTANT NOTE ---"
echo "This script does NOT uninstall .NET or Nginx, as they might be used by other applications."
echo "If you want to remove them, you can do so manually:"
echo " - To remove .NET SDK: sudo apt-get purge dotnet-sdk-8.0"
echo " - To remove Nginx:    sudo apt-get purge nginx nginx-common"
echo ""
