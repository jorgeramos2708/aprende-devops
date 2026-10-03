import { useParams } from 'react-router-dom'
import { Placeholder } from '../../components/Placeholder'

export function ExamResult() {
  const { attemptId } = useParams<{ attemptId: string }>()
  return (
    <Placeholder
      title="Resultado del examen"
      description={`El resultado del intento ${attemptId ?? ''} estará disponible cuando el motor de evaluación esté implementado en el API.`}
      backTo="/exams"
      backLabel="Volver a exámenes"
    />
  )
}
