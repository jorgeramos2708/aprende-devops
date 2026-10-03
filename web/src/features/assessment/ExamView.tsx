import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { Loader2, Send } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Button } from '../../components/Button'
import { Badge } from '../../components/Badge'
import { MarkdownRenderer } from '../../components/MarkdownRenderer'

interface ExamQuestion {
  questionId: string
  order: number
  prompt: string
  questionType: string
  options?: string[]
  maxScore: number
}

interface StartedExam {
  attemptId: string
  questions: ExamQuestion[]
  expiresAt: string
}

export function ExamView() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [started, setStarted] = useState<StartedExam | null>(null)
  const [answers, setAnswers] = useState<Record<string, string | string[]>>({})

  const start = useMutation({
    mutationFn: async () => (await api.post('/assessment/exams/start', { examId: id })).data as StartedExam,
    onSuccess: (data) => setStarted(data),
  })

  const submit = useMutation({
    mutationFn: async () => {
      if (!started) return
      const payload = {
        attemptId: started.attemptId,
        answers: started.questions.map((q) => ({
          questionId: q.questionId,
          answer: answers[q.questionId] ?? '',
          timeSpentSeconds: 0,
        })),
      }
      await api.post('/assessment/exams/submit', payload)
      navigate(`/exam/${started.attemptId}/result`)
    },
  })

  const setSingle = (qid: string, value: string) => setAnswers((a) => ({ ...a, [qid]: value }))

  const toggleMulti = (qid: string, value: string) => {
    setAnswers((a) => {
      const current = Array.isArray(a[qid]) ? (a[qid] as string[]) : []
      return { ...a, [qid]: current.includes(value) ? current.filter((v) => v !== value) : [...current, value] }
    })
  }

  if (!started) {
    return (
      <div className="max-w-2xl mx-auto text-center space-y-4">
        <h1 className="text-2xl font-bold text-dark-900">Presentar examen</h1>
        <p className="text-dark-600">Al iniciar, el intento queda registrado con límite de tiempo.</p>
        {start.isError && <p className="text-sm text-red-600">No se pudo iniciar el examen.</p>}
        <Button onClick={() => start.mutate()} loading={start.isPending}>
          <Send className="w-4 h-4 mr-2" /> Iniciar ahora
        </Button>
      </div>
    )
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-dark-900">Examen en curso</h1>
        <Badge variant="warning">Expira: {new Date(started.expiresAt).toLocaleTimeString()}</Badge>
      </div>

      {started.questions.map((q, i) => (
        <Card key={q.questionId}>
          <CardHeader>
            <CardTitle>{i + 1}. Pregunta ({q.maxScore} pts)</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <MarkdownRenderer content={q.prompt} />
            {(q.questionType === 'SingleChoice' || q.questionType === 'TrueFalse') && (
              <div className="space-y-2">
                {(q.options ?? []).map((opt) => (
                  <label key={opt} className="flex items-center gap-2 p-2 rounded-lg border border-dark-200 hover:border-primary-400 cursor-pointer">
                    <input
                      type="radio"
                      name={q.questionId}
                      checked={answers[q.questionId] === opt}
                      onChange={() => setSingle(q.questionId, opt)}
                    />
                    <span className="text-sm">{opt}</span>
                  </label>
                ))}
              </div>
            )}
            {q.questionType === 'MultipleChoice' && (
              <div className="space-y-2">
                {(q.options ?? []).map((opt) => (
                  <label key={opt} className="flex items-center gap-2 p-2 rounded-lg border border-dark-200 hover:border-primary-400 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={Array.isArray(answers[q.questionId]) && (answers[q.questionId] as string[]).includes(opt)}
                      onChange={() => toggleMulti(q.questionId, opt)}
                    />
                    <span className="text-sm">{opt}</span>
                  </label>
                ))}
              </div>
            )}
            {!['SingleChoice', 'TrueFalse', 'MultipleChoice'].includes(q.questionType) && (
              <textarea
                className="input"
                rows={3}
                placeholder="Escribe tu respuesta…"
                value={typeof answers[q.questionId] === 'string' ? (answers[q.questionId] as string) : ''}
                onChange={(e) => setSingle(q.questionId, e.target.value)}
              />
            )}
          </CardContent>
        </Card>
      ))}

      {submit.isError && <p className="text-sm text-red-600">No se pudo enviar el examen.</p>}
      <Button onClick={() => submit.mutate()} loading={submit.isPending} className="w-full">
        {submit.isPending ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : <Send className="w-4 h-4 mr-2" />}
        Enviar examen
      </Button>
    </div>
  )
}
