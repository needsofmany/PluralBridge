import os
from pathlib import Path
from openai import OpenAI

PROMPT_FILE = Path(__file__).with_name("pluralbridge-sweep-prompt.txt")
REPORT_FILE = Path(os.environ.get("PB_SWEEP_REPORT", "pluralbridge-sweep-report.md"))

def main() -> None:
    prompt = PROMPT_FILE.read_text(encoding="utf-8")
    client = OpenAI(api_key=os.environ["OPENAI_API_KEY"])

    response = client.responses.create(
        model=os.environ.get("OPENAI_MODEL", "gpt-5.5"),
        reasoning={"effort": "high"},
        tools=[
            {
                "type": "web_search",
                "search_context_size": "high",
            }
        ],
        tool_choice="required",
        input=prompt,
    )

    report = response.output_text.strip()
    REPORT_FILE.write_text(report + "\n", encoding="utf-8")
    print(report)

    github_step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if github_step_summary:
        with open(github_step_summary, "a", encoding="utf-8") as f:
            f.write("# PluralBridge Public Sweep\n\n")
            f.write(report)
            f.write("\n")

if __name__ == "__main__":
    main()
