import { Link } from 'react-router-dom'
import { Construction } from 'lucide-react'
import { Card, CardContent } from './Card'
import { Button } from './Button'

interface PlaceholderProps {
  title: string
  description: string
  backTo?: string
  backLabel?: string
}

export function Placeholder({ title, description, backTo = '/', backLabel = 'Volver al inicio' }: PlaceholderProps) {
  return (
    <div className="max-w-2xl mx-auto">
      <Card>
        <CardContent className="p-8 text-center">
          <Construction className="w-12 h-12 text-primary-500 mx-auto mb-4" />
          <h2 className="text-xl font-semibold text-dark-900 mb-2">{title}</h2>
          <p className="text-dark-600 mb-6">{description}</p>
          <Link to={backTo}>
            <Button variant="secondary">{backLabel}</Button>
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
