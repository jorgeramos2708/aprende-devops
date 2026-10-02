import { Routes, Route, Navigate } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './lib/auth'
import { Home } from './features/dashboard/Home'
import { Roadmap } from './features/learning/Roadmap'
import { TechnologyView } from './features/learning/TechnologyView'
import { LessonView } from './features/learning/LessonView'
import { LabView } from './features/labs/LabView'
import { Exams } from './features/assessment/Exams'
import { ExamView } from './features/assessment/ExamView'
import { ExamResult } from './features/assessment/ExamResult'
import { Certifications } from './features/dashboard/Certifications'
import { CertificationReadiness } from './features/dashboard/CertificationReadiness'
import { Insights } from './features/dashboard/Insights'
import { Profile } from './features/dashboard/Profile'
import { Login } from './features/auth/Login'
import { Register } from './features/auth/Register'
import { AdminDashboard } from './features/admin/AdminDashboard'
import { TechChanges } from './features/admin/TechChanges'
import { ImpactAssessments } from './features/admin/ImpactAssessments'
import { UpdateProposals } from './features/admin/UpdateProposals'
import { LabRegression } from './features/admin/LabRegression'

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      
      <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
        <Route path="/" element={<Home />} />
        <Route path="/roadmap" element={<Roadmap />} />
        <Route path="/technology/:slug" element={<TechnologyView />} />
        <Route path="/lesson/:id" element={<LessonView />} />
        <Route path="/lab/:id" element={<LabView />} />
        <Route path="/exams" element={<Exams />} />
        <Route path="/exam/:id" element={<ExamView />} />
        <Route path="/exam/:attemptId/result" element={<ExamResult />} />
        <Route path="/certifications" element={<Certifications />} />
        <Route path="/certification/:id/readiness" element={<CertificationReadiness />} />
        <Route path="/insights" element={<Insights />} />
        <Route path="/profile" element={<Profile />} />
        
        <Route path="/admin/*" element={<ProtectedRoute allowedRoles={['admin']}><AdminDashboard /></ProtectedRoute>}>
          <Route path="tech-changes" element={<TechChanges />} />
          <Route path="impact" element={<ImpactAssessments />} />
          <Route path="proposals" element={<UpdateProposals />} />
          <Route path="regression" element={<LabRegression />} />
        </Route>
      </Route>
      
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
