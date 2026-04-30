---
name: security-audit
description: Deep security audit for AISportCoach - checks authentication, file upload, API security, secrets, SQL injection, and .NET-specific vulnerabilities
runsOn: [conversation]
---

You are conducting a **comprehensive security audit** of the AISportCoach codebase - an AI-powered tennis coaching platform with video upload, analysis, and user authentication.

## Security Focus Areas

Review the current changes and codebase for these vulnerability classes, prioritized by risk:

### 1. Authentication & Authorization (CRITICAL)
- [ ] JWT token validation - check signature verification, expiration, claims validation
- [ ] Refresh token security - rotation, secure storage, proper invalidation
- [ ] WebAuthn implementation - challenge generation, credential verification, user handle privacy
- [ ] Password storage - verify bcrypt/PBKDF2 usage with proper iterations (never plain text or weak hashing)
- [ ] Session management - fixation, hijacking, timeout policies
- [ ] Authorization checks - verify every endpoint checks user permissions/ownership
- [ ] Insecure Direct Object References (IDOR) - users can only access their own videos/reports

### 2. File Upload Vulnerabilities (CRITICAL)
- [ ] File size limits enforced (check against `VideoStorage:MaxFileSizeMB`)
- [ ] File type validation - verify extension AND magic bytes/MIME type
- [ ] Path traversal - no `..` in filenames, use safe path combination
- [ ] Malicious content scanning - video files could contain exploits
- [ ] Storage location - files stored outside webroot, not directly accessible
- [ ] Filename sanitization - prevent command injection via filenames
- [ ] Denial of Service - rate limiting on uploads, async processing

### 3. API Security (HIGH)
- [ ] Rate limiting on all endpoints (especially /upload, /analyze, /auth)
- [ ] CORS configuration - verify allowed origins (not `*` in production)
- [ ] Input validation - all DTOs have proper attributes, range checks
- [ ] Mass assignment - DTOs don't expose internal/admin fields
- [ ] Error disclosure - `ProblemDetails` doesn't leak stack traces in production
- [ ] API versioning - deprecated endpoints properly sunset
- [ ] Request size limits - prevent payload bombs

### 4. Secrets & Configuration (HIGH)
- [ ] No API keys in appsettings.json or code (`Gemini:ApiKey` must be in User Secrets/env vars)
- [ ] No database connection strings committed
- [ ] Secrets not logged (check structured logging for accidental leaks)
- [ ] Production config uses secure defaults (HTTPS only, secure cookies)

### 5. SQL Injection & Data Access (MEDIUM - EF Core mitigates most)
- [ ] All queries use parameterized EF Core LINQ (no string interpolation in `FromSqlRaw`)
- [ ] User input never directly concatenated into SQL
- [ ] Repository pattern properly isolates data access
- [ ] No `EnsureCreated` in production (use migrations only)

### 6. Injection Attacks (MEDIUM)
- [ ] Command injection - check `GeminiFileService` and any shell execution
- [ ] XSS - API returns JSON only, but check if HTML ever returned
- [ ] JSON injection - properly escape user input before passing to Gemini prompts
- [ ] Log injection - structured logging used, not string concatenation

### 7. Dependency & Supply Chain (MEDIUM)
- [ ] Check for known vulnerable NuGet packages: `dotnet list package --vulnerable`
- [ ] Review transitive dependencies
- [ ] Semantic Kernel version - check for known issues
- [ ] ASP.NET Core version - verify latest security patches

### 8. Cryptography (MEDIUM)
- [ ] No custom crypto implementations
- [ ] Random values use `RandomNumberGenerator` not `Random`
- [ ] Token generation uses cryptographically secure RNG
- [ ] TLS 1.2+ enforced for external API calls (Gemini)

### 9. Information Disclosure (LOW)
- [ ] Exception middleware doesn't expose internals in production
- [ ] API versioning headers don't leak framework versions
- [ ] Health check endpoints don't expose sensitive details
- [ ] Logging doesn't include PII without sanitization

### 10. Business Logic (LOW)
- [ ] Users can't bypass subscription tier limits
- [ ] Video analysis can't be triggered without proper authorization
- [ ] NTRP ratings can't be manipulated client-side
- [ ] Concurrent request handling - no race conditions in report generation

## Audit Workflow

1. **Check git status** for changed files - prioritize API, auth, upload, middleware
2. **Read authentication code** - token service, WebAuthn, refresh tokens
3. **Read file upload handlers** - `UploadVideoHandler`, validation middleware
4. **Read API controllers** - authorization attributes, input validation
5. **Read configuration** - check for hardcoded secrets in appsettings.*.json
6. **Grep for dangerous patterns**:
   - `FromSqlRaw` with string interpolation
   - `Process.Start`, `cmd.exe`, shell execution
   - Hardcoded passwords/keys: `apikey`, `password =`, `secret =`
   - Weak crypto: `new Random()`, `MD5`, `SHA1` for passwords
7. **Check middleware pipeline** - auth before controllers, exception handling properly configured
8. **Review dependencies**: Run `dotnet list package --vulnerable`

## Reporting Format

For each finding, provide:

```markdown
### [SEVERITY] Issue Title

**Location:** `src/Path/To/File.cs:123`

**Issue:** Brief description of the vulnerability

**Risk:** What an attacker could do

**Fix:**
```csharp
// Secure implementation
```

**Priority:** [Critical/High/Medium/Low]
```

Group findings by severity. Start with CRITICAL issues.

## .NET-Specific Security Checklist

- [ ] `[ValidateAntiForgeryToken]` on state-changing endpoints (if using cookies)
- [ ] `[Authorize]` attribute on all non-public endpoints
- [ ] `RequireHttpsMetadata = true` in JWT bearer options
- [ ] No `[AllowAnonymous]` on sensitive endpoints
- [ ] `CancellationToken` prevents hung requests becoming DoS vector
- [ ] `ModelState.IsValid` checked (or `[ApiController]` auto-validates)
- [ ] No `dynamic` types with untrusted input
- [ ] Async methods don't swallow cancellation

## Excluded Patterns (Per CLAUDE.md)

These are NOT security issues in this codebase:
- Primary constructors for DI (standard pattern here)
- Repository pattern instead of DbContext in handlers (design choice)
- No AutoMapper (manual DTOs preferred)
- No stored procedures (EF Core only)

## Final Output

Provide a prioritized list of findings, starting with any CRITICAL issues. If no issues found, state:

> ✅ **Security audit complete.** No critical vulnerabilities detected in the reviewed changes. Recommended: run `dotnet list package --vulnerable` to check dependencies.
