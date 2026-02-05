#!/bin/bash

# Batch Nametag Generator
# Reads a CSV file and generates nametags for each person
# Downloads Roblox avatars when usernames are available

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AVATARS_DIR="$SCRIPT_DIR/avatars"
OUTPUT_DIR="$SCRIPT_DIR/output"
DEFAULT_IMAGE="$SCRIPT_DIR/docs/Generic-Roblox-Character.png"

# Check dependencies
if ! command -v jq &> /dev/null; then
    echo "Error: jq is required but not installed. Install with: brew install jq"
    exit 1
fi

# Check for CSV file argument
if [ -z "$1" ]; then
    echo "Usage: $0 <csv-file>"
    echo "Example: $0 Reports-Justin.csv"
    exit 1
fi

CSV_FILE="$1"

if [ ! -f "$CSV_FILE" ]; then
    echo "Error: CSV file not found: $CSV_FILE"
    exit 1
fi

# Create output directories
mkdir -p "$AVATARS_DIR"
mkdir -p "$OUTPUT_DIR"

# Function to lookup Roblox user ID from username
lookup_user_id() {
    local username="$1"
    local response
    
    response=$(curl -s -X POST "https://users.roblox.com/v1/usernames/users" \
        -H "Content-Type: application/json" \
        -d "{\"usernames\":[\"$username\"]}")
    
    # Extract user ID from response
    local user_id
    user_id=$(echo "$response" | jq -r '.data[0].id // empty')
    
    if [ -z "$user_id" ] || [ "$user_id" = "null" ]; then
        echo ""
        return 1
    fi
    
    echo "$user_id"
}

# Function to download avatar image
# Returns the path to the avatar image on stdout, logs to stderr
download_avatar() {
    local user_id="$1"
    local username="$2"
    local output_path="$AVATARS_DIR/${username}.png"
    
    # Check if avatar is already cached
    if [ -f "$output_path" ]; then
        echo "  [CACHED] Avatar already exists: $output_path" >&2
        echo "$output_path"
        return 0
    fi
    
    echo "  [DOWNLOAD] Downloading avatar for $username (ID: $user_id)..." >&2
    
    # First, get the image URL from the Thumbnails API
    local thumbnail_response
    thumbnail_response=$(curl -s "https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds=${user_id}&size=420x420&format=Png&isCircular=false")
    
    local image_url
    image_url=$(echo "$thumbnail_response" | jq -r '.data[0].imageUrl // empty')
    
    if [ -z "$image_url" ] || [ "$image_url" = "null" ]; then
        echo "  [ERROR] Could not get avatar URL from API" >&2
        echo ""
        return 1
    fi
    
    # Download the avatar from the CDN URL
    curl -s -L "$image_url" -o "$output_path"
    
    # Verify download succeeded (check it's actually a PNG, not HTML)
    if [ -f "$output_path" ] && [ -s "$output_path" ]; then
        local file_type
        file_type=$(file -b "$output_path" | head -c 3)
        if [ "$file_type" = "PNG" ]; then
            echo "$output_path"
            return 0
        else
            echo "  [ERROR] Downloaded file is not a PNG image" >&2
            rm -f "$output_path"
            echo ""
            return 1
        fi
    else
        echo "  [ERROR] Failed to download avatar" >&2
        rm -f "$output_path"
        echo ""
        return 1
    fi
}

# Function to sanitize filename (remove special characters)
sanitize_filename() {
    echo "$1" | tr -cd '[:alnum:] ._-' | tr ' ' '_'
}

# Process CSV file
echo "Processing CSV file: $CSV_FILE"
echo "================================"

# Read CSV, skipping header row
line_num=0
while IFS=',' read -r employee_name schema job_title email seat start_date tenure roblox_username || [ -n "$employee_name" ]; do
    line_num=$((line_num + 1))
    
    # Skip header row
    if [ $line_num -eq 1 ]; then
        continue
    fi
    
    # Skip empty lines
    if [ -z "$employee_name" ]; then
        continue
    fi
    
    # Trim whitespace and carriage returns from fields
    employee_name="${employee_name#"${employee_name%%[![:space:]]*}"}"
    employee_name="${employee_name%"${employee_name##*[![:space:]]}"}"
    schema="${schema#"${schema%%[![:space:]]*}"}"
    schema="${schema%"${schema##*[![:space:]]}"}"
    email="${email#"${email%%[![:space:]]*}"}"
    email="${email%"${email##*[![:space:]]}"}"
    roblox_username="${roblox_username#"${roblox_username%%[![:space:]]*}"}"
    roblox_username="${roblox_username%"${roblox_username##*[![:space:]]}"}"
    roblox_username=$(echo "$roblox_username" | tr -d '\r')
    
    echo ""
    echo "[$line_num] Processing: $employee_name"
    
    # Determine image path
    image_path="$DEFAULT_IMAGE"
    username_arg=""
    
    if [ -n "$roblox_username" ]; then
        echo "  Roblox username: $roblox_username"
        
        # Lookup user ID (validates username exists)
        user_id=$(lookup_user_id "$roblox_username")
        
        if [ -n "$user_id" ]; then
            echo "  User ID: $user_id"
            
            # Download or use cached avatar
            avatar_path=$(download_avatar "$user_id" "$roblox_username")
            
            if [ -n "$avatar_path" ] && [ -f "$avatar_path" ]; then
                image_path="$avatar_path"
                username_arg="--username \"$roblox_username\""
            else
                echo "  [FALLBACK] Using default image"
            fi
        else
            echo "  [WARNING] Could not find Roblox user: $roblox_username"
            echo "  [FALLBACK] Using default image"
        fi
    else
        echo "  No Roblox username provided, using default image"
    fi
    
    # Generate output filename
    safe_name=$(sanitize_filename "$employee_name")
    output_file="$OUTPUT_DIR/${safe_name}.pdf"
    
    # Run nametag generator
    echo "  Generating nametag: $output_file"
    
    # Build the command
    cmd="dotnet run --project \"$SCRIPT_DIR/jryan-nametag.csproj\" -- \
        --name \"$employee_name\" \
        --team \"$schema\" \
        --image \"$image_path\" \
        --quote \"$email\" \
        --output \"$output_file\""
    
    # Add username if available
    if [ -n "$username_arg" ]; then
        cmd="$cmd $username_arg"
    fi
    
    # Execute the command
    eval $cmd
    
    if [ $? -eq 0 ]; then
        echo "  [SUCCESS] Generated: $output_file"
    else
        echo "  [ERROR] Failed to generate nametag for $employee_name"
    fi
    
done < "$CSV_FILE"

echo ""
echo "================================"
echo "Done! Output files are in: $OUTPUT_DIR"
