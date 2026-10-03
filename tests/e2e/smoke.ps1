$base = "http://localhost:18080"
$fail = 0
function Check($name, $cond, $extra = "") {
  if ($cond) { Write-Output "PASS $name $extra" } else { Write-Output "FAIL $name $extra"; $script:fail++ }
}
function Post($url, $body, $token = "") {
  $h = @{ "Content-Type" = "application/json" }
  if ($token) { $h["Authorization"] = "Bearer $token" }
  Invoke-RestMethod -Uri "$base$url" -Method Post -Headers $h -Body ($body | ConvertTo-Json -Depth 6)
}
function Get($url, $token = "") {
  $h = @{}
  if ($token) { $h["Authorization"] = "Bearer $token" }
  Invoke-RestMethod -Uri "$base$url" -Method Get -Headers $h
}

# 1. Registro + login + me
$reg = Post "/api/auth/register" @{ email = "smoke@test.com"; password = "Secreta123"; displayName = "Smoke" }
Check "register" ($reg.accessToken.Length -gt 50 -and $reg.user.email -eq "smoke@test.com")
$tok = $reg.accessToken
$me = Get "/api/auth/me" $tok
Check "me" ($me.displayName -eq "Smoke" -and $me.roles -contains "student")
$dup = try { Post "/api/auth/register" @{ email = "smoke@test.com"; password = "Secreta123"; displayName = "X" }; "NO-CONFLICT" } catch { $_.Exception.Response.StatusCode.value__ }
Check "register-duplicado-409" ($dup -eq 409) "($dup)"
$bad = try { Post "/api/auth/login" @{ email = "smoke@test.com"; password = "mal" }; "NO-401" } catch { $_.Exception.Response.StatusCode.value__ }
Check "login-mal-401" ($bad -eq 401) "($bad)"
$ref = Post "/api/auth/refresh" @{ refreshToken = $reg.refreshToken }
Check "refresh-rotate" ($ref.accessToken.Length -gt 50 -and $ref.refreshToken -ne $reg.refreshToken)
$tok = $ref.accessToken
$old = try { Post "/api/auth/refresh" @{ refreshToken = $reg.refreshToken }; "NO-401" } catch { $_.Exception.Response.StatusCode.value__ }
Check "refresh-viejo-revocado-401" ($old -eq 401) "($old)"

# 2. Roadmap + tecnologias + leccion
$road = Get "/api/learning/roadmap" $tok
Check "roadmap-3-techs" (($road.technologies | Measure-Object).Count -eq 3)
$tech = Get "/api/learning/technologies/linux" $tok
Check "tech-linux" ($tech.name -eq "Linux")
$nodeId = $tech.nodes[0].id
$lesson = Get "/api/learning/nodes/$nodeId" $tok
Check "lesson-markdown" ($lesson.content.Contains("terminal"))
Check "lesson-labId" ($null -ne $lesson.labId)

# 3. Labs
$labs = Get "/api/labs/" $tok
Check "labs-lista" (($labs.items | Measure-Object).Count -ge 1)
$lab = Get "/api/labs/$($labs.items[0].id)" $tok
Check "lab-detalle" ($lab.name.Length -gt 0)

# 4. Progreso
$prog = Post "/api/learning/progress" @{ nodeId = $nodeId; status = "completed"; score = 95; timeSpentSeconds = 600 } $tok
Check "progress" ($prog.status -eq "completed" -and $prog.score -eq 95)
$skills = Get "/api/learning/skills" $tok
Check "skills" (($skills | Measure-Object).Count -ge 1)

# 5. Examenes: listar, iniciar, responder todo bien, enviar, resultado
$exams = Get "/api/assessment/exams" $tok
Check "exams-lista" (($exams | Measure-Object).Count -ge 1)
$start = Post "/api/assessment/exams/start" @{ examId = $exams[0].id } $tok
Check "exam-start-3q" (($start.questions | Measure-Object).Count -eq 3)
function Responder($preguntas, $modo) {
  $preguntas | ForEach-Object {
    $p = $_.prompt
    if ($modo -eq "bien") {
      if ($p.Contains("ocultos")) { $r = "ls -a" }
      elseif ($p.Contains("directorio actual")) { $r = "pwd" }
      elseif ($p.Contains("755")) { $r = "true" }
      else { $r = "" }
    } else {
      $r = "respuesta-mala"
    }
    @{ questionId = $_.questionId; answer = $r; timeSpentSeconds = 10 }
  }
}
$ans = Responder $start.questions "bien"
$sub = Post "/api/assessment/exams/submit" @{ attemptId = $start.attemptId; answers = $ans } $tok
Check "exam-100-pass" ($sub.percentage -eq 100 -and $sub.passed -eq $true) "($($sub.percentage)%)"
$res = Get "/api/assessment/exams/$($start.attemptId)/result" $tok
Check "exam-result" ($res.passed -eq $true -and ($res.questionResults | Measure-Object).Count -eq 3)

# 6. Examen reprobado genera insight
$start2 = Post "/api/assessment/exams/start" @{ examId = $exams[0].id } $tok
$ans2 = Responder $start2.questions "mal"
$sub2 = Post "/api/assessment/exams/submit" @{ attemptId = $start2.attemptId; answers = $ans2 } $tok
Check "exam-reprobado" ($sub2.passed -eq $false)
$ins = Get "/api/insights" $tok
Check "insight-debilidad" ((($ins | Where-Object { $_.type -eq "weakness" }) | Measure-Object).Count -ge 1)
$id0 = $ins[0].id
Post "/api/insights/$id0/read" @{} $tok | Out-Null
$ins2 = Get "/api/insights" $tok
Check "insight-leido" ((($ins2 | Where-Object { $_.id -eq $id0 })[0].isRead) -eq $true)

# 7. Certificaciones + readiness
$certs = Get "/api/certifications" $tok
Check "certs-lista" (($certs | Measure-Object).Count -ge 1)
$ready = Get "/api/certifications/$($certs[0].id)/readiness" $tok
Check "readiness" ($ready.overallReadiness -ge 0 -and ($ready.domains | Measure-Object).Count -eq 2)

# 8. Admin sin rol -> 403; anonimo -> 401
$adm = try { Get "/api/admin/tech-changes" $tok; "NO-403" } catch { $_.Exception.Response.StatusCode.value__ }
Check "admin-403-estudiante" ($adm -eq 403) "($adm)"
$anon = try { Get "/api/learning/roadmap"; "NO-401" } catch { $_.Exception.Response.StatusCode.value__ }
Check "anon-401" ($anon -eq 401) "($anon)"

# 9. Watcher check con fuente inexistente no rompe (0 fuentes activas con red? hay 2 seed) - solo valida 200
Write-Output "TOTAL_FALLOS=$fail"
