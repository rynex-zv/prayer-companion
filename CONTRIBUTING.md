# Contributing to Prayer Companion

Thanks for your interest in improving Prayer Companion / Pray Ad Free.

## Contribution policy

All external or collaborative code changes must go through a **Pull Request** and maintainer review before they are merged into `Alpha`.

Please **do not push directly to `Alpha`**. Work in a fork or a separate branch and open a Pull Request when the change is ready for review.

The project maintainer has final approval over what is merged into the repository.

## Workflow

1. Fork the repository, or create a feature branch if you already have repository access.
2. Branch from the latest `Alpha`.
3. Keep each Pull Request focused on one feature, fix, or logical change.
4. Test the affected code before submitting.
5. Open a Pull Request against `Alpha`.
6. Explain what changed, why it changed, and how you tested it.
7. Address review comments when requested.
8. Wait for maintainer approval before merge.

Suggested branch names:

```text
feature/qibla-improvement
fix/prayer-notification
refactor/location-service
docs/build-instructions
```

## Pull Request expectations

A useful Pull Request should include:

- A clear title
- A concise description of the problem and solution
- Relevant screenshots for UI changes
- Testing or reproduction steps when applicable
- No unrelated formatting or refactoring mixed into the change
- No generated files changed unless the change requires them

Large architectural changes should be discussed in an Issue before significant implementation work begins.

## Review and merge

Submitting code does not mean the code will be merged.

The maintainer may:

- approve the Pull Request;
- request changes;
- ask for additional tests or documentation;
- close a Pull Request that does not fit the project direction.

**Only reviewed and explicitly accepted changes should be merged into `Alpha`.**

## Code ownership

The repository uses a `CODEOWNERS` file assigning repository-wide ownership to `@rynex-zv`.

For this to be enforced automatically, GitHub branch protection or a repository ruleset should require Pull Requests and require Code Owner approval before merging.

## Licensing and attribution of contributions

The repository is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. Project-authored material may also carry the reasonable author-attribution term documented in [ATTRIBUTION.md](ATTRIBUTION.md).

By submitting a contribution for inclusion in this repository, you agree that the contribution may be distributed as part of the project under AGPL-3.0 and the project notices applicable to the files/work into which it is incorporated, unless the maintainer explicitly agrees otherwise in writing. You may identify and preserve your own authorship credit for your contribution.

Do not remove existing copyright, license, project-attribution, or third-party notices without a documented reason accepted during review.

## Third-party code and assets

Do not contribute code, media, fonts, audio, translations, datasets, icons, images, or other material that you do not have the legal right to submit and redistribute.

If a Pull Request adds or materially changes a third-party dependency or asset, it must also update [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when attribution or license documentation is applicable.

For new third-party material, include in the Pull Request:

- the exact component/asset name;
- the original source;
- the copyright holder/author when known;
- the license or written redistribution permission;
- any required attribution wording;
- whether the material is modified.

For audio, photographs, artwork, and similar media, a public download URL is **not** proof of redistribution permission. The source and rights must be documented.

## Security issues

Please avoid publishing sensitive credentials, private keys, tokens, personal data, or exploitable secrets in Issues or Pull Requests.

If a change accidentally contains a secret, revoke or rotate the secret immediately; removing it from a later commit is not enough.