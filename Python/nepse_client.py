import json
import sys
from nepse import Nepse


def main():
    nepse = Nepse()

    # Temporary workaround for Nepse SSL issue
    nepse.setTLSVerification(False)

    # Example: get company list
    companies = nepse.getCompanyList()

    # Print JSON so .NET can read it from stdout
    print(json.dumps(companies, indent=2))


if __name__ == "__main__":
    main()