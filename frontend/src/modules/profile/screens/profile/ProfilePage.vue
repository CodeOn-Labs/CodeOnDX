<script setup lang="ts">
// Port ProfilePage.tsx — Hồ sơ cá nhân: ảnh đại diện (thu nhỏ 250px trước khi gửi), thông tin cơ bản, kỹ năng (sửa được),
// dự án đang tham gia, lộ trình đồng hành. Thêm (D4): đổi mật khẩu. Ngôn ngữ hiển thị đổi bằng nút cờ ở Sidebar (components/layout/LanguageSwitcher.vue).
import { computed, reactive, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import {
  Activity, Award, Building2, Calendar, Camera, Check, CheckCircle2, Clock, Code, Edit3, KeyRound, Mail, Phone, Plus, User, X, XCircle,
} from '@lucide/vue';
import PageLayout from '@/components/layout/PageLayout.vue';
import Button from '@/shared/components/Button.vue';
import FormItem from '@/shared/components/form/FormItem.vue';
import BaseInput from '@/shared/components/form/BaseInput.vue';
import { accountApi } from '@/modules/auth/api/auth.api';
import { ApiError } from '@/api/http';
import { useAuthStore } from '@/stores/auth';
import { useToastStore } from '@/stores/toast';
import { getPastelAvatarProps } from '@/shared/utils/avatar';
import { formatDate } from '@/shared/utils/date';
import { cn } from '@/shared/utils/cn';

const { t, locale } = useI18n();
const auth = useAuthStore();
const toast = useToastStore();
const queryClient = useQueryClient();

const query = useQuery({ queryKey: computed(() => ['profile', locale.value] as const), queryFn: ({ signal }) => accountApi.profile(signal) });
const profile = computed(() => query.data.value);
const displayName = computed(() => profile.value?.fullName || profile.value?.email || '');
const avatar = computed(() => getPastelAvatarProps(displayName.value, profile.value?.id));
const isActive = computed(() => profile.value?.employee?.isActive ?? true);

async function update(input: Parameters<typeof accountApi.updateProfile>[0]) {
  const res = await accountApi.updateProfile(input);
  queryClient.setQueryData(['profile', locale.value], res.data);
  auth.patchUser({ fullName: res.data.fullName, avatarUrl: res.data.avatarUrl, preferredLocale: res.data.preferredLocale });
  toast.success(res.messageText);
  return res.data;
}

// ─── Ảnh đại diện: thu nhỏ tối đa 250×250, JPEG 85% (giống bản gốc) ───────
const fileInput = ref<HTMLInputElement | null>(null);
function onAvatarChange(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onloadend = () => {
    const img = new Image();
    img.onload = async () => {
      const MAX = 250;
      const scale = Math.min(1, MAX / Math.max(img.width, img.height));
      const canvas = document.createElement('canvas');
      canvas.width = Math.round(img.width * scale);
      canvas.height = Math.round(img.height * scale);
      canvas.getContext('2d')?.drawImage(img, 0, 0, canvas.width, canvas.height);
      try {
        await update({ avatarUrl: canvas.toDataURL('image/jpeg', 0.85) });
      } catch (err) {
        toast.error((err as Error).message);
      }
    };
    img.src = reader.result as string;
  };
  reader.readAsDataURL(file);
}

// ─── Kỹ năng ────────────────────────────────────────────────────────────────
const editingSkills = ref(false);
const skills = ref<string[]>([]);
const newSkill = ref('');
const savingSkills = ref(false);
watch(profile, (p) => (skills.value = [...(p?.employee?.skills ?? [])]), { immediate: true });
function addSkill() {
  const v = newSkill.value.trim();
  if (v && !skills.value.some((s) => s.toLowerCase() === v.toLowerCase())) skills.value.push(v);
  newSkill.value = '';
}
async function saveSkills() {
  savingSkills.value = true;
  try {
    await update({ skills: skills.value });
    editingSkills.value = false;
  } catch (err) {
    toast.error((err as Error).message);
  } finally {
    savingSkills.value = false;
  }
}
function cancelSkills() {
  skills.value = [...(profile.value?.employee?.skills ?? [])];
  editingSkills.value = false;
}

// ─── Đổi mật khẩu ───────────────────────────────────────────────────────────
const pw = reactive({ currentPassword: '', password: '', confirmPassword: '' });
const pwErrors = ref<Record<string, string>>({});
const savingPw = ref(false);
async function changePassword() {
  const e: Record<string, string> = {};
  if (!pw.currentPassword) e.currentPassword = t('validation.required');
  if (!pw.password) e.password = t('validation.required');
  if (pw.confirmPassword !== pw.password) e.confirmPassword = t('profile.err_password_mismatch');
  pwErrors.value = e;
  if (Object.keys(e).length) return;
  savingPw.value = true;
  try {
    toast.success((await accountApi.changePassword(pw.currentPassword, pw.password, pw.confirmPassword)).messageText);
    Object.assign(pw, { currentPassword: '', password: '', confirmPassword: '' });
  } catch (err) {
    if (err instanceof ApiError && err.status === 400) pwErrors.value = Object.fromEntries(Object.entries(err.fieldErrors).map(([k, v]) => [k, v[0] ?? '']));
    else toast.error((err as Error).message);
  } finally {
    savingPw.value = false;
  }
}

const infoItems = computed(() => {
  const p = profile.value;
  if (!p) return [];
  return [
    { icon: Mail, label: t('profile.lbl_email'), value: p.email },
    { icon: Phone, label: t('profile.lbl_phone'), value: p.phone },
    { icon: User, label: t('profile.lbl_code'), value: p.employee?.code },
    { icon: Building2, label: t('profile.lbl_department'), value: [p.employee?.department?.name, p.employee?.team?.name].filter(Boolean).join(' / ') },
    { icon: Award, label: t('profile.lbl_level'), value: p.employee?.level?.name },
    { icon: Calendar, label: t('profile.lbl_joined_date'), value: formatDate(p.employee?.joinedDate) },
    { icon: Activity, label: t('profile.lbl_status'), value: isActive.value ? t('profile.lbl_tenure_active') : t('profile.lbl_tenure_resigned') },
  ].filter((i) => i.value);
});
const CARD = 'border border-slate-200/50 bg-white/60 backdrop-blur-md rounded-3xl p-6 shadow-sm transition-all duration-300 hover:shadow-md hover:bg-white/80';
</script>

<template>
  <PageLayout layout="C" content-padding="24px" disable-scroll>
    <div v-if="query.isPending.value" class="flex flex-col gap-6 animate-pulse">
      <div class="bg-white/60 border border-slate-200/50 rounded-3xl p-6 flex items-center gap-4">
        <div class="w-20 h-20 bg-slate-200 rounded-[2rem]" />
        <div class="space-y-2"><div class="h-5 w-40 bg-slate-200 rounded" /><div class="h-3.5 w-24 bg-slate-200 rounded" /></div>
      </div>
      <div class="flex gap-6"><div class="w-[320px] h-64 bg-white/60 rounded-3xl" /><div class="flex-1 h-64 bg-white/60 rounded-3xl" /></div>
    </div>

    <div v-else-if="profile" class="flex flex-col h-full relative">
      <div class="absolute top-0 left-1/4 w-72 h-72 bg-indigo-100/10 rounded-full blur-3xl pointer-events-none z-0" />
      <div class="z-40 -mx-6 -mt-6 mb-6 bg-white/80 px-6 pt-4 pb-4 backdrop-blur-xl border-b border-slate-100">
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div class="flex items-center gap-5 min-w-0">
            <div class="relative w-16 h-16 select-none shrink-0 group">
              <div :class="cn('relative w-full h-full rounded-[1.8rem] flex items-center justify-center text-2xl font-black shadow-sm border z-10 overflow-hidden', !profile.avatarUrl ? avatar.colorClass : 'bg-white border-slate-200')">
                <img v-if="profile.avatarUrl" :src="profile.avatarUrl" :alt="displayName" class="w-full h-full object-cover" />
                <template v-else>{{ avatar.initial }}</template>
              </div>
              <button
                type="button"
                class="absolute inset-0 z-20 flex items-center justify-center bg-black/40 text-white rounded-[1.8rem] opacity-0 group-hover:opacity-100 focus-visible:opacity-100 transition-opacity cursor-pointer backdrop-blur-[2px]"
                :title="$t('profile.btn_change_avatar')"
                :aria-label="$t('profile.btn_change_avatar')"
                @click="fileInput?.click()"
              >
                <Camera :size="20" />
              </button>
              <input ref="fileInput" type="file" class="hidden" accept="image/jpeg, image/png, image/webp" @change="onAvatarChange" />
            </div>
            <div>
              <h1 class="text-xl font-black text-slate-800 leading-tight tracking-tight">{{ displayName }}</h1>
              <div class="flex items-center gap-2 text-[11px] font-bold text-slate-500 uppercase tracking-widest mt-1 truncate">
                <span class="truncate">{{ profile.employee?.position?.name ?? $t('profile.lbl_unknown_position') }}</span>
                <template v-if="profile.employee?.level">
                  <span class="w-1 h-1 rounded-full bg-slate-300 shrink-0" />
                  <span class="truncate">{{ profile.employee.level.name }}</span>
                </template>
              </div>
            </div>
          </div>
          <div class="flex items-center gap-2 flex-wrap">
            <span v-for="r in profile.roles" :key="r.id" class="text-[10px] font-black uppercase tracking-wider text-slate-400 bg-slate-50 border border-slate-200/60 px-3 py-1.5 rounded-full select-none">{{ r.name }}</span>
          </div>
        </div>
      </div>

      <div class="flex-1 overflow-y-auto min-h-0 pb-8 pr-1 z-10 relative">
        <div class="flex flex-col lg:flex-row gap-6 items-start">
          <div class="w-full lg:w-[320px] xl:w-[340px] shrink-0 flex flex-col gap-6 lg:sticky lg:top-2">
            <div :class="cn(CARD, 'flex flex-col relative overflow-hidden')">
              <h3 class="text-[11px] font-black text-slate-400 uppercase tracking-widest mb-4 border-b border-slate-100/60 pb-2">{{ $t('profile.title_basic_info') }}</h3>
              <div v-for="item in infoItems" :key="item.label" class="flex items-center justify-between w-full py-2.5 border-b border-slate-100/50 last:border-0 group">
                <div class="flex items-center gap-3 text-slate-500 group-hover:text-slate-700">
                  <div class="w-8 h-8 rounded-xl bg-slate-50/50 border border-slate-100/80 flex items-center justify-center shadow-sm group-hover:bg-white transition-all duration-300 group-hover:scale-105">
                    <component :is="item.icon" :size="14" :stroke-width="2.5" />
                  </div>
                  <span class="text-[10px] font-bold tracking-wide uppercase">{{ item.label }}</span>
                </div>
                <span class="text-[12.5px] font-black text-slate-800 truncate max-w-[170px] text-right" :title="item.value ?? ''">{{ item.value }}</span>
              </div>
            </div>
          </div>

          <div class="flex-1 flex flex-col gap-6 w-full min-w-0">
            <!-- Kỹ năng -->
            <div :class="CARD">
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase flex items-center gap-2.5"><Code :size="16" class="text-indigo-500" />{{ $t('profile.title_skills') }}</h3>
                <Button v-if="!editingSkills && profile.employee" variant="ghost" size="sm" @click="editingSkills = true">
                  <template #leftIcon><Edit3 :size="14" /></template>
                  {{ $t('profile.btn_edit_skills') }}
                </Button>
              </div>
              <div v-if="!profile.employee" class="text-[12px] italic text-slate-400">{{ $t('profile.hint_no_employee') }}</div>
              <template v-else-if="editingSkills">
                <div class="flex flex-wrap gap-2 mb-3">
                  <span v-for="s in skills" :key="s" class="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-3 py-1 text-[12px] font-bold text-indigo-600 ring-1 ring-indigo-100">
                    {{ s }}
                    <button type="button" class="rounded-full p-0.5 hover:bg-indigo-100" :aria-label="$t('profile.btn_remove_skill')" @click="skills = skills.filter((x) => x !== s)"><X :size="11" /></button>
                  </span>
                </div>
                <div class="flex items-center gap-2">
                  <BaseInput v-model="newSkill" :placeholder="$t('profile.ph_new_skill')" class="h-9" @keydown.enter.prevent="addSkill" />
                  <Button variant="outline" size="sm" @click="addSkill"><template #leftIcon><Plus :size="14" /></template>{{ $t('profile.btn_add_skill') }}</Button>
                </div>
                <div class="mt-4 flex justify-end gap-2">
                  <Button variant="ghost" size="sm" @click="cancelSkills">{{ $t('common.btn_cancel') }}</Button>
                  <Button variant="primary" size="sm" :loading="savingSkills" @click="saveSkills"><template #leftIcon><Check :size="14" /></template>{{ $t('common.btn_save') }}</Button>
                </div>
              </template>
              <div v-else class="flex flex-wrap gap-2">
                <span v-for="s in profile.employee.skills" :key="s" class="rounded-full bg-indigo-50 px-3 py-1 text-[12px] font-bold text-indigo-600 ring-1 ring-indigo-100">{{ s }}</span>
                <span v-if="profile.employee.skills.length === 0" class="text-[12px] italic text-slate-400">{{ $t('profile.empty_no_skill') }}</span>
              </div>
            </div>

            <!-- Dự án -->
            <div :class="CARD">
              <div class="flex justify-between items-center mb-5">
                <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase flex items-center gap-2.5"><Activity :size="16" class="text-indigo-500" />{{ $t('profile.title_projects') }}</h3>
                <span class="text-[10px] text-indigo-600 font-extrabold bg-indigo-50/80 border border-indigo-100/60 px-3 py-1 rounded-full uppercase tracking-wider">{{ $t('profile.lbl_project_count', { count: profile.projects.length }) }}</span>
              </div>
              <div class="grid gap-3 sm:grid-cols-2">
                <RouterLink
                  v-for="p in profile.projects"
                  :key="p.id"
                  :to="`/projects/${p.id}`"
                  class="flex items-center gap-3.5 p-4 rounded-2xl border border-slate-200/60 bg-white/60 shadow-sm transition-all duration-300 hover:shadow-md hover:bg-white hover:border-indigo-300 hover:-translate-y-0.5 group"
                >
                  <div class="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-50 to-indigo-100/50 border border-indigo-100 flex items-center justify-center text-indigo-600"><Building2 :size="16" /></div>
                  <div class="min-w-0">
                    <h4 class="text-[13px] font-black text-slate-800 truncate group-hover:text-indigo-600">{{ p.name }}</h4>
                    <span class="text-[9px] text-slate-400 font-black uppercase tracking-widest">{{ p.code }}</span>
                  </div>
                </RouterLink>
                <div v-if="profile.projects.length === 0" class="sm:col-span-2 text-[13px] text-slate-500 font-medium py-8 flex flex-col items-center gap-2 border border-dashed border-slate-200 rounded-2xl">
                  <Calendar :size="20" class="text-slate-400" />
                  {{ $t('profile.empty_no_project') }}
                </div>
              </div>
            </div>

            <!-- Lộ trình -->
            <div v-if="profile.employee" :class="CARD">
              <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase mb-6 flex items-center gap-2.5"><Clock :size="16" class="text-indigo-500" />{{ $t('profile.title_timeline') }}</h3>
              <div class="relative flex justify-between items-center w-full py-4 px-1 select-none">
                <div class="absolute left-6 right-6 top-1/2 -translate-y-1/2 h-[3px] bg-slate-100 rounded-full z-0 overflow-hidden">
                  <div :class="cn('h-full rounded-full bg-gradient-to-r', isActive ? 'from-emerald-400 to-indigo-500' : 'from-slate-300 to-slate-400')" />
                </div>
                <div class="flex flex-col items-center z-10">
                  <div :class="cn('w-10 h-10 rounded-2xl flex items-center justify-center border shadow-sm bg-white', isActive ? 'border-emerald-200 text-emerald-500' : 'border-slate-200 text-slate-400')"><Calendar :size="16" /></div>
                  <span class="text-[8px] text-slate-400 uppercase font-black tracking-widest mt-2">{{ $t('profile.lbl_join') }}</span>
                  <span class="text-[10px] font-extrabold text-slate-800 mt-0.5">{{ formatDate(profile.employee.joinedDate) || '--/--/----' }}</span>
                </div>
                <div class="z-10 px-2.5 py-0.5 rounded-full border text-[8px] font-black uppercase tracking-wider shadow-sm bg-white" :class="isActive ? 'border-indigo-100 text-indigo-600' : 'border-slate-200 text-slate-500'">
                  {{ isActive ? $t('profile.lbl_tenure_active') : $t('profile.lbl_tenure_resigned') }}
                </div>
                <div class="flex flex-col items-center z-10">
                  <div :class="cn('w-10 h-10 rounded-2xl flex items-center justify-center border shadow-sm bg-white', isActive ? 'border-indigo-200 text-indigo-500' : 'border-rose-200 text-rose-500')">
                    <CheckCircle2 v-if="isActive" :size="16" />
                    <XCircle v-else :size="16" />
                  </div>
                  <span class="text-[8px] text-slate-400 uppercase font-black tracking-widest mt-2">{{ $t('profile.lbl_status') }}</span>
                  <span class="text-[10px] font-extrabold text-slate-800 mt-0.5">{{ isActive ? $t('profile.lbl_present') : formatDate(profile.employee.leftDate) }}</span>
                </div>
              </div>
            </div>

            <!-- Đổi mật khẩu -->
            <div :class="CARD">
              <h3 class="text-[14px] font-black text-slate-800 tracking-wider uppercase mb-5 flex items-center gap-2.5"><KeyRound :size="16" class="text-indigo-500" />{{ $t('profile.title_change_password') }}</h3>
              <form class="grid gap-4 md:grid-cols-3" novalidate @submit.prevent="changePassword">
                <FormItem :label="$t('profile.lbl_current_password')" :error="pwErrors.currentPassword">
                  <BaseInput v-model="pw.currentPassword" type="password" autocomplete="current-password" :error="!!pwErrors.currentPassword" />
                </FormItem>
                <FormItem :label="$t('profile.lbl_new_password')" :error="pwErrors.password">
                  <BaseInput v-model="pw.password" type="password" autocomplete="new-password" :error="!!pwErrors.password" />
                </FormItem>
                <FormItem :label="$t('profile.lbl_confirm_password')" :error="pwErrors.confirmPassword">
                  <BaseInput v-model="pw.confirmPassword" type="password" autocomplete="new-password" :error="!!pwErrors.confirmPassword" />
                </FormItem>
                <div class="md:col-span-3 flex justify-end">
                  <Button type="submit" variant="primary" size="sm" :loading="savingPw">{{ $t('profile.btn_change_password') }}</Button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  </PageLayout>
</template>
