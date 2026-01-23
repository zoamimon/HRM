#!/bin/bash

# =============================================
# Script: Run All Identity Module Migrations
# Purpose: Execute all SQL migration scripts in correct order
# Usage: ./run-all-migrations.sh [server] [database] [username] [password]
# =============================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values (override with arguments)
SERVER="${1:-localhost}"
DATABASE="${2:-HrmDb}"
USERNAME="${3:-sa}"
PASSWORD="${4:-YourStrong@Passw0rd}"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Identity Module Migration Script${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Configuration:"
echo "  Server: $SERVER"
echo "  Database: $DATABASE"
echo "  Username: $USERNAME"
echo ""

# Check if sqlcmd is available
if ! command -v sqlcmd &> /dev/null; then
    echo -e "${RED}ERROR: sqlcmd not found${NC}"
    echo "Please install SQL Server command-line tools:"
    echo "  - Linux: https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-setup-tools"
    echo "  - macOS: brew install mssql-tools"
    echo "  - Windows: Installed with SQL Server"
    exit 1
fi

# Test connection
echo -e "${YELLOW}Testing database connection...${NC}"
if ! sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -d master -Q "SELECT 1" &> /dev/null; then
    echo -e "${RED}ERROR: Cannot connect to SQL Server${NC}"
    echo "Please check:"
    echo "  1. SQL Server is running"
    echo "  2. Server name is correct: $SERVER"
    echo "  3. Credentials are correct"
    exit 1
fi
echo -e "${GREEN}✓ Connection successful${NC}"
echo ""

# Create database if not exists
echo -e "${YELLOW}Ensuring database exists...${NC}"
sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -d master -Q "
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'$DATABASE')
BEGIN
    CREATE DATABASE [$DATABASE]
    PRINT 'Database created'
END
ELSE
BEGIN
    PRINT 'Database already exists'
END
" || { echo -e "${RED}Failed to create database${NC}"; exit 1; }
echo ""

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# List of migration scripts in order
SCRIPTS=(
    "001_CreateOperatorsTable.sql"
    "002_CreateIndexes.sql"
    "003_SeedAdminOperator.sql"
    "004_CreateRefreshTokensTable.sql"
    "005_MigrateRefreshTokensToPolymorphic.sql"
)

# Execute each script
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Executing Migration Scripts${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

EXECUTED=0
SKIPPED=0
FAILED=0

for SCRIPT in "${SCRIPTS[@]}"; do
    SCRIPT_PATH="$SCRIPT_DIR/$SCRIPT"

    if [ ! -f "$SCRIPT_PATH" ]; then
        echo -e "${RED}✗ Script not found: $SCRIPT${NC}"
        FAILED=$((FAILED + 1))
        continue
    fi

    echo -e "${YELLOW}[$((EXECUTED + SKIPPED + FAILED + 1))/${#SCRIPTS[@]}] Executing: $SCRIPT${NC}"

    if sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -d "$DATABASE" -i "$SCRIPT_PATH" > /dev/null 2>&1; then
        echo -e "${GREEN}  ✓ Success${NC}"
        EXECUTED=$((EXECUTED + 1))
    else
        # Script might have checks that skip execution (e.g., "table already exists")
        # Try again with output to see if it's a skip or error
        OUTPUT=$(sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -d "$DATABASE" -i "$SCRIPT_PATH" 2>&1)

        if echo "$OUTPUT" | grep -qi "already exists\|Skipping"; then
            echo -e "${YELLOW}  ⚠ Skipped (already exists)${NC}"
            SKIPPED=$((SKIPPED + 1))
        else
            echo -e "${RED}  ✗ Failed${NC}"
            echo "$OUTPUT"
            FAILED=$((FAILED + 1))
        fi
    fi
    echo ""
done

# Summary
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Migration Summary${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "  Total Scripts: ${#SCRIPTS[@]}"
echo -e "  ${GREEN}Executed: $EXECUTED${NC}"
echo -e "  ${YELLOW}Skipped: $SKIPPED${NC}"
echo -e "  ${RED}Failed: $FAILED${NC}"
echo ""

if [ $FAILED -gt 0 ]; then
    echo -e "${RED}❌ Migration completed with errors${NC}"
    exit 1
else
    echo -e "${GREEN}✅ Migration completed successfully!${NC}"
    echo ""
    echo "Next steps:"
    echo "  1. Verify tables exist:"
    echo "     sqlcmd -S $SERVER -U $USERNAME -P '$PASSWORD' -d $DATABASE -Q \"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='Identity'\""
    echo ""
    echo "  2. Verify admin operator exists:"
    echo "     sqlcmd -S $SERVER -U $USERNAME -P '$PASSWORD' -d $DATABASE -Q \"SELECT Username, Email FROM Identity.Operators WHERE Username='admin'\""
    echo ""
    echo "  3. Start your application and test login"
fi
