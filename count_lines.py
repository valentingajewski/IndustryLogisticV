import os
import glob

def count_lines(file_path):
    """Counts lines in a file, handling potential encoding issues gracefully."""
    try:
        with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
            return sum(1 for _ in f)
    except Exception as e:
        return f"Error reading file: {e}"

def generate_markdown_report(output_filename="file_line_counts.md"):
    """Lists all files in the current directory and subdirectories with line counts."""
    # Fetch all files recursively from the current directory
    all_files = glob.glob('**/*', recursive=True)
    
    # Filter out directories
    files_only = [f for f in all_files if os.path.isfile(f)]
    
    # Avoid writing the output markdown file details into itself if it already exists
    files_only = [f for f in files_only if os.path.basename(f) != output_filename]

    markdown_lines = [
        "# File Line Count Report\n",
        "Generated automatically by Python script.\n",
        "| File Path | Line Count |",
        "| :--- | :--- |"
    ]
    
    print(f"Scanning directory and counting lines for {len(files_only)} files...")
    
    for file_path in sorted(files_only):
        lines = count_lines(file_path)
        # Use forward slashes for consistent markdown formatting across systems
        formatted_path = file_path.replace('\\', '/')
        markdown_lines.append(f"| {formatted_path} | {lines} |")
        
    with open(output_filename, 'w', encoding='utf-8') as f:
        f.write('\n'.join(markdown_lines))
        
    print(f"Report successfully saved to {output_filename}")

if __name__ == "__main__":
    generate_markdown_report()