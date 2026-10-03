import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Loader2, CheckCircle, XCircle } from 'lucide-react'
import { api } from '../../lib/api'
import { Card, CardHeader, CardTitle, CardContent } from '../../components/Card'
import { Badge } from '../../components/Badge'

interface QuestionResult {
  questionId: string
  correct: boolean
  score: number
  maxScore: number
  explanation?: string
}

interface ExamResultData {
  attemptId: string
  score: number
  maxScore: number
  percentage: number
  passed: boolean
  questionResults: QuestionResult[]
  gradedAt: string
}

export function ExamResult() {
  const { attemptId } = useParams<{ attemptId: string }>()
  const { data, isLoading, isError } = useQuery<ExamResultData>({
    queryKey: ['exam-result', attemptId],
    queryFn: async () => (await api.get(`/assessment/exams/${attemptId}/result`)).data,
    enabled: !!attemptId,
  })

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 text-dark-500">
        <Loader2 className="w-5 h-5 animate-spin" /> Cargando resultado…
      </div>
    )
  }

  if (isError || !data) {
    return <p className="text-sm text-red-600">No se pudo cargar el resultado.</p>
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <Card>
        <CardContent className="p-6 text-center">
          {data.passed ? (
            <CheckCircle className="w-12 h-12 text-green-600 mx-auto mb-2" />
          ) : (
            <XCircle className="w-12 h-12 text-red-500 mx-auto mb-2" />
          )}
          <h1 className="text-2xl font-bold text-dark-900">
            {data.score}/{data.maxScore} ({data.percentage}%)
          </h1>
          <Badge variant={data.passed ? 'success' : 'danger'}>{data.passed ? 'Aprobado' : 'No aprobado'}</Badge>
        </CardContent>
      </Card>

      {data.questionResults.map((q, i) => (
        <Card key={q.questionId}>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle>Pregunta {i + 1}</CardTitle>
              <Badge variant={q.correct ? 'success' : 'danger'}>
                {q.score}/{q.maxScore}
              </Badge>
            </div>
          </CardHeader>
          {q.explanation && (
            <CardContent>
              <p className="text-sm text-dark-600">{q.explanation}</p>
            </CardContent>
          )}
        </Card>
      ))}

      <Link to="/exams" className="text-sm text-primary-600 hover:text-primary-700 font-medium">
        ← Volver a exámenes
      </Link>
    </div>
  )
}
