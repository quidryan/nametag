#!/usr/bin/env python3
"""
Batch Nametag Generator

Reads a CSV file and generates nametags for each person.
Downloads Roblox avatars when usernames are available.

Usage: python3 generate-nametags.py <csv-file>
"""

import csv
import json
import subprocess
import sys
import urllib.request
import urllib.error
from pathlib import Path


# Configuration
SCRIPT_DIR = Path(__file__).parent.resolve()
AVATARS_DIR = SCRIPT_DIR / "avatars"
OUTPUT_DIR = SCRIPT_DIR / "output"
DEFAULT_IMAGE = SCRIPT_DIR / "docs" / "Generic-Roblox-Character.png"


def lookup_user_id(username: str) -> int | None:
    """Look up Roblox user ID from username."""
    url = "https://users.roblox.com/v1/usernames/users"
    data = json.dumps({"usernames": [username]}).encode("utf-8")
    
    try:
        req = urllib.request.Request(
            url,
            data=data,
            headers={"Content-Type": "application/json"}
        )
        with urllib.request.urlopen(req, timeout=30) as response:
            result = json.loads(response.read().decode("utf-8"))
            if result.get("data") and len(result["data"]) > 0:
                return result["data"][0].get("id")
    except (urllib.error.URLError, json.JSONDecodeError) as e:
        print(f"  [ERROR] Failed to lookup user ID: {e}")
    
    return None


def download_avatar(user_id: int, username: str) -> Path | None:
    """Download avatar image, using cache if available."""
    output_path = AVATARS_DIR / f"{username}.png"
    
    # Check cache
    if output_path.exists():
        print(f"  [CACHED] Avatar already exists: {output_path}")
        return output_path
    
    print(f"  [DOWNLOAD] Downloading avatar for {username} (ID: {user_id})...")
    
    # Get avatar URL from Thumbnails API
    thumbnail_url = (
        f"https://thumbnails.roblox.com/v1/users/avatar-headshot"
        f"?userIds={user_id}&size=420x420&format=Png&isCircular=false"
    )
    
    try:
        with urllib.request.urlopen(thumbnail_url, timeout=30) as response:
            result = json.loads(response.read().decode("utf-8"))
            if not result.get("data") or len(result["data"]) == 0:
                print("  [ERROR] No avatar data returned from API")
                return None
            
            image_url = result["data"][0].get("imageUrl")
            if not image_url:
                print("  [ERROR] No image URL in response")
                return None
        
        # Download the actual image
        with urllib.request.urlopen(image_url, timeout=30) as response:
            image_data = response.read()
        
        # Verify it's a PNG
        if not image_data.startswith(b'\x89PNG'):
            print("  [ERROR] Downloaded file is not a PNG image")
            return None
        
        # Save to cache
        output_path.write_bytes(image_data)
        return output_path
        
    except (urllib.error.URLError, json.JSONDecodeError, OSError) as e:
        print(f"  [ERROR] Failed to download avatar: {e}")
        return None


def sanitize_filename(name: str) -> str:
    """Remove special characters and replace spaces with underscores."""
    allowed = set("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ._-")
    sanitized = "".join(c for c in name if c in allowed)
    return sanitized.replace(" ", "_")


def generate_nametag(
    name: str,
    team: str,
    image_path: Path,
    quote: str,
    output_path: Path,
    username: str | None = None
) -> bool:
    """Run dotnet to generate a nametag PDF."""
    cmd = [
        "dotnet", "run",
        "--project", str(SCRIPT_DIR / "jryan-nametag.csproj"),
        "--",
        "--name", name,
        "--team", team,
        "--image", str(image_path),
        "--quote", quote,
        "--output", str(output_path)
    ]
    
    if username:
        cmd.extend(["--username", username])
    
    try:
        result = subprocess.run(
            cmd,
            cwd=SCRIPT_DIR,
            capture_output=True,
            text=True,
            timeout=60
        )
        return result.returncode == 0
    except subprocess.TimeoutExpired:
        print("  [ERROR] dotnet command timed out")
        return False
    except Exception as e:
        print(f"  [ERROR] Failed to run dotnet: {e}")
        return False


def process_csv(csv_path: Path) -> None:
    """Process a CSV file and generate nametags for each person."""
    print(f"Processing CSV file: {csv_path}")
    print("=" * 40)
    
    # Create output directories
    AVATARS_DIR.mkdir(exist_ok=True)
    OUTPUT_DIR.mkdir(exist_ok=True)
    
    with open(csv_path, "r", encoding="utf-8") as f:
        reader = csv.reader(f)
        header = next(reader)  # Skip header
        
        for line_num, row in enumerate(reader, start=2):
            if len(row) < 4:
                continue
            
            # Extract fields (indices: 0=Name, 1=Schema, 3=Email, 7=RobloxUsername)
            employee_name = row[0].strip()
            schema = row[1].strip()
            email = row[3].strip() if len(row) > 3 else ""
            roblox_username = row[7].strip() if len(row) > 7 else ""
            
            if not employee_name:
                continue
            
            print(f"\n[{line_num}] Processing: {employee_name}")
            
            # Determine image path and username
            image_path = DEFAULT_IMAGE
            username_for_nametag = None
            
            if roblox_username:
                print(f"  Roblox username: {roblox_username}")
                
                # Lookup user ID (validates username exists)
                user_id = lookup_user_id(roblox_username)
                
                if user_id:
                    print(f"  User ID: {user_id}")
                    
                    # Download or use cached avatar
                    avatar_path = download_avatar(user_id, roblox_username)
                    
                    if avatar_path and avatar_path.exists():
                        image_path = avatar_path
                        username_for_nametag = roblox_username
                    else:
                        print("  [FALLBACK] Using default image")
                else:
                    print(f"  [WARNING] Could not find Roblox user: {roblox_username}")
                    print("  [FALLBACK] Using default image")
            else:
                print("  No Roblox username provided, using default image")
            
            # Generate output filename
            safe_name = sanitize_filename(employee_name)
            output_file = OUTPUT_DIR / f"{safe_name}.pdf"
            
            print(f"  Generating nametag: {output_file}")
            
            success = generate_nametag(
                name=employee_name,
                team=schema,
                image_path=image_path,
                quote=email,
                output_path=output_file,
                username=username_for_nametag
            )
            
            if success:
                print(f"  [SUCCESS] Generated: {output_file}")
            else:
                print(f"  [ERROR] Failed to generate nametag for {employee_name}")
    
    print("\n" + "=" * 40)
    print(f"Done! Output files are in: {OUTPUT_DIR}")


def main() -> int:
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} <csv-file>")
        print(f"Example: {sys.argv[0]} Reports-Justin.csv")
        return 1
    
    csv_path = Path(sys.argv[1])
    
    if not csv_path.exists():
        print(f"Error: CSV file not found: {csv_path}")
        return 1
    
    if not DEFAULT_IMAGE.exists():
        print(f"Error: Default image not found: {DEFAULT_IMAGE}")
        return 1
    
    process_csv(csv_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
