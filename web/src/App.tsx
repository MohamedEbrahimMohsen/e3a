import { useEffect } from 'react';
import { Outlet, Route, Routes, useLocation } from 'react-router-dom';
import { AuthProvider } from './app/AuthContext';
import { ReportProvider } from './app/ReportContext';
import { ToastProvider } from './app/ToastContext';
import { Footer } from './components/Footer';
import { NavBar } from './components/NavBar';
import { CatalogPage } from './features/catalog/CatalogPage';
import { EngineerComposerPage } from './features/composer/EngineerComposerPage';
import { TeamComposerPage } from './features/composer/TeamComposerPage';
import { EngineerDetailPage } from './features/detail/EngineerDetailPage';
import { TeamDetailPage } from './features/detail/TeamDetailPage';
import { HomePage } from './features/home/HomePage';
import { HowItWorksPage } from './features/how/HowItWorksPage';
import { NotFoundPage } from './features/notfound/NotFoundPage';
import { ProfilePage } from './features/profile/ProfilePage';
import { PublishStatusPage } from './features/publish/PublishStatusPage';
import { WorkspacePage } from './features/workspace/WorkspacePage';

function ScrollToTop() {
  const location = useLocation();
  useEffect(() => { window.scrollTo(0, 0); }, [location.pathname]);
  return null;
}

function StandardLayout() {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <NavBar />
      <Outlet />
      <Footer />
    </div>
  );
}

function ComposerLayout() {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Outlet />
    </div>
  );
}

export function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <ReportProvider>
          <ScrollToTop />
          <Routes>
            <Route element={<StandardLayout />}>
              <Route path="/" element={<HomePage />} />
              <Route path="/catalog" element={<CatalogPage />} />
              <Route path="/e/:name" element={<EngineerDetailPage />} />
              <Route path="/t/:name" element={<TeamDetailPage />} />
              <Route path="/u/:login" element={<ProfilePage />} />
              <Route path="/how" element={<HowItWorksPage />} />
              <Route path="/workspace" element={<WorkspacePage />} />
              <Route path="/workspace/publish" element={<PublishStatusPage />} />
              <Route path="*" element={<NotFoundPage />} />
            </Route>
            <Route element={<ComposerLayout />}>
              <Route path="/workspace/new-engineer" element={<EngineerComposerPage />} />
              <Route path="/workspace/new-team" element={<TeamComposerPage />} />
            </Route>
          </Routes>
        </ReportProvider>
      </AuthProvider>
    </ToastProvider>
  );
}
