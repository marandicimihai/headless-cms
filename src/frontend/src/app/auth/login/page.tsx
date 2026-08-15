import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel, FieldSeparator } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import Link from "next/link";

export default function LoginForm() {
    return (
        <Card className="w-full max-w-sm">
            <CardHeader>
                <CardTitle className="w-full flex justify-center font-semibold">
                    Login to cms
                </CardTitle>
            </CardHeader>
            <CardContent>
                <form>
                    <FieldGroup>
                        <Field>
                            <FieldLabel htmlFor="email">Email</FieldLabel>
                            <Input
                                id="email"
                                type="email"
                                placeholder="user@example.com"
                                required
                            />
                        </Field>
                        <Field>
                            <FieldLabel htmlFor="pwd">Password</FieldLabel>
                            <Input
                                id="pwd"
                                type="password"
                                required
                            />
                        </Field>
                        <Field className="my-2">
                            <Button type="submit">
                                Login
                            </Button>
                        </Field>
                        <p className="text-center text-xs text-muted-foreground">
                            Accounts are created through workspace invitations.
                        </p>
                    </FieldGroup>
                </form>
            </CardContent>
        </Card>
    )
}